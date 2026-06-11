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
$toolsSource = Join-Path $root "tools"
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

Publish-Project $project

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
$toolsOutput = Join-Path $output "tools"
$webView2UserData = Join-Path $output "UnityCodeGraphLauncher.exe.WebView2"

Remove-GeneratedDirectory $webOutput
Remove-GeneratedDirectory $toolsOutput
Remove-GeneratedDirectory $webView2UserData

Copy-Item -LiteralPath $webSource -Destination $webOutput -Recurse -Force
New-Item -ItemType Directory -Force -Path $toolsOutput | Out-Null
Copy-Item -LiteralPath (Join-Path $toolsSource "static-server.mjs") -Destination $toolsOutput -Force

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
