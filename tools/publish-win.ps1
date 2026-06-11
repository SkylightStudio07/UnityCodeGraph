param(
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [switch] $SelfContained
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "UnityCodeGraph\UnityCodeGraph.csproj"
$launcherProject = Join-Path $root "UnityCodeGraph.Launcher\UnityCodeGraph.Launcher.csproj"
$output = Join-Path $root "dist\UnityCodeGraph-$Runtime"

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

Write-Host ""
Write-Host "Published to $output"
Write-Host "Run:"
Write-Host "  $output\UnityCodeGraphLauncher.exe"
Write-Host "  $output\UnityCodeGraph.exe <UnityProjectRoot> --roots Scripts,Source --output graph.json"
Write-Host "  $output\UnityCodeGraph.exe <UnityProjectRoot> --roots Scripts,Source --watch --output graph.json"
