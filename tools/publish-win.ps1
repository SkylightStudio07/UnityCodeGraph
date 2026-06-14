param(
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [switch] $SelfContained,
    [switch] $Zip
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "UnityCodeGraph\UnityCodeGraph.csproj"
$launcherProject = Join-Path $root "UnityCodeGraph.Launcher\UnityCodeGraph.Launcher.csproj"
$webSource = Join-Path $root "web"
$output = Join-Path $root "dist\UnityCodeGraph-$Runtime"

function Remove-GeneratedDirectory {
    param(
        [string] $Path
    )

    if (-not (Test-Path $Path)) {
        return
    }

    $resolvedOutput = (Resolve-Path $output).Path
    $resolvedTarget = (Resolve-Path $Path).Path
    if (-not $resolvedTarget.StartsWith($resolvedOutput, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove directory outside publish output: $resolvedTarget"
    }

    Remove-Item -LiteralPath $resolvedTarget -Recurse -Force
}

function Publish-Project {
    param(
        [string] $ProjectPath
    )

    $arguments = @(
        "publish",
        $ProjectPath,
        "-c", $Configuration,
        "-r", $Runtime,
        "-o", $output,
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:DebugType=none",
        "-p:DebugSymbols=false"
    )

    if ($SelfContained) {
        $arguments += "--self-contained"
        $arguments += "true"
    } else {
        $arguments += "--self-contained"
        $arguments += "false"
    }

    dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
}

function Assert-LauncherNotRunning {
    $launcherExe = Join-Path $output "UnityCodeGraphLauncher.exe"
    if (-not (Test-Path $launcherExe)) {
        return
    }

    $resolvedLauncher = (Resolve-Path $launcherExe).Path
    $running = Get-Process UnityCodeGraphLauncher -ErrorAction SilentlyContinue | Where-Object {
        try {
            $_.Path -and $_.Path.Equals($resolvedLauncher, [StringComparison]::OrdinalIgnoreCase)
        } catch {
            $false
        }
    }

    if ($running) {
        $ids = ($running | Select-Object -ExpandProperty Id) -join ", "
        throw "Close the running launcher before publishing. Locked file: $launcherExe (process id: $ids)"
    }
}

Publish-Project $project
Assert-LauncherNotRunning

$launcherArguments = @(
    "publish",
    $launcherProject,
    "-c", $Configuration,
    "-r", $Runtime,
    "-o", $output,
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:DebugType=none",
    "-p:DebugSymbols=false"
)

if ($SelfContained) {
    $launcherArguments += "--self-contained"
    $launcherArguments += "true"
} else {
    $launcherArguments += "--self-contained"
    $launcherArguments += "false"
}

dotnet @launcherArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$webOutput = Join-Path $output "web"
$webView2UserData = Join-Path $output "UnityCodeGraphLauncher.exe.WebView2"

Remove-GeneratedDirectory $webOutput
Remove-GeneratedDirectory $webView2UserData

Copy-Item -LiteralPath $webSource -Destination $webOutput -Recurse -Force

if ($Zip) {
    $zipPath = Join-Path (Split-Path -Parent $output) "UnityCodeGraph-$Runtime.zip"
    if (Test-Path $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $output "*") -DestinationPath $zipPath -Force
}

Write-Host ""
Write-Host "Published to $output"
if ($Zip) {
    Write-Host "Zipped to $zipPath"
}
Write-Host "Run:"
Write-Host "  $output\UnityCodeGraphLauncher.exe"
Write-Host "  $output\UnityCodeGraph.exe <UnityProjectRoot> --roots Scripts,Source --output graph.json"
Write-Host "  $output\UnityCodeGraph.exe <UnityProjectRoot> --roots Scripts,Source --watch --output graph.json"
