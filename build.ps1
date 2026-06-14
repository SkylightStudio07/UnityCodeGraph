param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Debug",
    [string] $Runtime = "win-x64",
    [switch] $Release,
    [switch] $Verify,
    [switch] $Publish,
    [switch] $SelfContained,
    [switch] $Zip
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$cliProject = Join-Path $root "UnityCodeGraph\UnityCodeGraph.csproj"
$launcherProject = Join-Path $root "UnityCodeGraph.Launcher\UnityCodeGraph.Launcher.csproj"
$publishScript = Join-Path $root "tools\publish-win.ps1"
$verifyScript = Join-Path $root "tools\verify-analysis.ps1"
$webSource = Join-Path $root "web"
$distOutput = Join-Path $root "dist\UnityCodeGraph-$Runtime"

if ($Release) {
    $Configuration = "Release"
}

function Invoke-Step {
    param(
        [string] $Name,
        [scriptblock] $Command
    )

    Write-Host ""
    Write-Host "==> $Name"
    & $Command
}

function Assert-LastExitCode {
    param(
        [string] $Name
    )

    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE"
    }
}

function Sync-WebAssetsToDist {
    $webOutput = Join-Path $distOutput "web"
    if (-not (Test-Path $distOutput)) {
        Write-Host "Skipped dist web sync; publish output does not exist yet: $distOutput"
        return
    }

    if (Test-Path $webOutput) {
        Remove-Item -LiteralPath $webOutput -Recurse -Force
    }

    Copy-Item -LiteralPath $webSource -Destination $webOutput -Recurse -Force
    Write-Host "Synced web assets to $webOutput"
}

Push-Location $root
try {
    if ($Publish) {
        $arguments = @(
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-File", $publishScript,
            "-Configuration", $Configuration,
            "-Runtime", $Runtime
        )

        if ($SelfContained) {
            $arguments += "-SelfContained"
        }

        if ($Zip) {
            $arguments += "-Zip"
        }

        Invoke-Step "Publish $Configuration $Runtime" {
            powershell @arguments
            Assert-LastExitCode "publish"
        }
    } else {
        Invoke-Step "Build CLI ($Configuration)" {
            dotnet build $cliProject -c $Configuration
            Assert-LastExitCode "CLI build"
        }

        Invoke-Step "Build launcher ($Configuration)" {
            dotnet build $launcherProject -c $Configuration
            Assert-LastExitCode "Launcher build"
        }

        Invoke-Step "Sync web assets to dist" {
            Sync-WebAssetsToDist
        }
    }

    Invoke-Step "Check web JavaScript" {
        node --check (Join-Path $root "web\app.js")
        Assert-LastExitCode "web JavaScript check"
    }

    Invoke-Step "Check launcher JavaScript" {
        node --check (Join-Path $root "UnityCodeGraph.Launcher\app\app.js")
        Assert-LastExitCode "launcher JavaScript check"
    }

    if ($Verify) {
        Invoke-Step "Verify analysis fixture" {
            powershell -NoProfile -ExecutionPolicy Bypass -File $verifyScript
            Assert-LastExitCode "analysis verification"
        }
    }

    Write-Host ""
    Write-Host "Build shortcut completed."
} finally {
    Pop-Location
}
