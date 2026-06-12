param(
    [switch] $KeepTemp
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "UnityCodeGraph\UnityCodeGraph.csproj"
$tempRoot = Join-Path $root ".tmp-analysis-verify"
$fixtureRoot = Join-Path $tempRoot "Fixture"
$output = Join-Path $tempRoot "graph.json"

function Assert-True {
    param(
        [bool] $Condition,
        [string] $Message
    )

    if (-not $Condition) {
        throw "Assertion failed: $Message"
    }
}

function Remove-TempRoot {
    if (-not (Test-Path -LiteralPath $tempRoot)) {
        return
    }

    $resolvedRoot = (Resolve-Path -LiteralPath $root).Path
    $resolvedTemp = (Resolve-Path -LiteralPath $tempRoot).Path
    if (-not $resolvedTemp.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove temp directory outside workspace: $resolvedTemp"
    }

    Remove-Item -LiteralPath $resolvedTemp -Recurse -Force
}

function Find-Edge {
    param(
        [object[]] $Edges,
        [string] $Source,
        [string] $Kind,
        [string] $Target
    )

    @($Edges) | Where-Object {
        $_.Source -eq $Source -and $_.Kind -eq $Kind -and $_.Target -eq $Target
    } | Select-Object -First 1
}

function Find-MethodEdge {
    param(
        [object[]] $Edges,
        [string] $SourcePattern,
        [string] $TargetPattern
    )

    @($Edges) | Where-Object {
        $_.Source -like $SourcePattern -and $_.Target -like $TargetPattern
    } | Select-Object -First 1
}

try {
    Remove-TempRoot
    New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null

    Set-Content -LiteralPath (Join-Path $fixtureRoot "Consumer.cs") -Encoding UTF8 -Value @'
using Game.Models;
using System.Collections.Generic;

namespace Feature;

public sealed class Consumer<T> where T : Target
{
    private Target field = new Target();

    public void Run(object maybe)
    {
        var local = new Target();
        var fetched = GetComponent<Target>();

        if (maybe is Target matched)
        {
            matched.Ping();
        }

        field = maybe as Target;
        this.field.Ping();
        local.Ping();
        fetched.Ping();

        foreach (Target item in GetTargets())
        {
            item.Ping();
        }

        Helper.Touch();
    }

    private static TComponent GetComponent<TComponent>() where TComponent : Target => default!;

    private static IEnumerable<Target> GetTargets()
    {
        yield return new Target();
    }
}
'@

    Set-Content -LiteralPath (Join-Path $fixtureRoot "GameTarget.cs") -Encoding UTF8 -Value @'
namespace Game.Models;

public sealed class Target
{
    public void Ping()
    {
    }
}
'@

    Set-Content -LiteralPath (Join-Path $fixtureRoot "Helper.cs") -Encoding UTF8 -Value @'
namespace Game.Models;

public static class Helper
{
    public static void Touch()
    {
    }
}
'@

    Set-Content -LiteralPath (Join-Path $fixtureRoot "UiTarget.cs") -Encoding UTF8 -Value @'
namespace Ui.Models;

public sealed class Target
{
    public void Ping()
    {
    }
}
'@

    Write-Host "[verify] Running analyzer fixture..."
    & dotnet run --project $project -- $fixtureRoot --output $output
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet run failed with exit code $LASTEXITCODE"
    }

    $graph = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
    $nodes = @($graph.Nodes)
    $edges = @($graph.Edges)
    $methodEdges = @($graph.MethodEdges)

    $consumer = "Feature.Consumer"
    $target = "Game.Models.Target"
    $uiTarget = "Ui.Models.Target"
    $helper = "Game.Models.Helper"

    Assert-True (($nodes | Where-Object { $_.Id -eq $consumer }) -ne $null) "Consumer node should exist."
    Assert-True (($nodes | Where-Object { $_.Id -eq $target }) -ne $null) "Game target node should exist."
    Assert-True (($nodes | Where-Object { $_.Id -eq $uiTarget }) -ne $null) "UI target node should exist."
    Assert-True (($nodes | Where-Object { $_.Id -eq $helper }) -ne $null) "Helper node should exist."

    Assert-True ((Find-Edge $edges $consumer "type_constraint" $target) -ne $null) "Generic constraints should resolve through using directives."
    Assert-True ((Find-Edge $edges $consumer "has_field_type" $target) -ne $null) "Field types should resolve through using directives."
    Assert-True ((Find-Edge $edges $consumer "creates" $target) -ne $null) "Object creation should target the imported type."
    Assert-True ((Find-Edge $edges $consumer "type_check" $target) -ne $null) "is/as checks should create type_check edges."
    Assert-True ((Find-Edge $edges $consumer "uses_local_type" $target) -ne $null) "foreach variable types should create local type edges."
    Assert-True ((Find-Edge $edges $consumer "unity_get_component" $target) -ne $null) "GetComponent<T>() should create Unity component edges."
    Assert-True ((Find-Edge $edges $consumer "calls_member" $helper) -ne $null) "Static type receiver calls should create calls_member edges."

    $targetCall = Find-Edge $edges $consumer "calls_member" $target
    Assert-True ($targetCall -ne $null) "Instance method receiver calls should create calls_member edges."
    Assert-True ([int]$targetCall.Weight -ge 5) "Instance receiver calls should be counted across locals, fields, pattern variables, and foreach variables."

    Assert-True ((Find-MethodEdge $methodEdges "Feature.Consumer.Run@*" "Game.Models.Target.Ping@*") -ne $null) "Method call graph should connect Run to Target.Ping."
    Assert-True ((Find-MethodEdge $methodEdges "Feature.Consumer.Run@*" "Game.Models.Helper.Touch@*") -ne $null) "Method call graph should connect Run to Helper.Touch."

    $wrongEdges = @($edges | Where-Object { $_.Source -eq $consumer -and $_.Target -eq $uiTarget })
    Assert-True ($wrongEdges.Count -eq 0) "Using-aware resolution should not connect Consumer to Ui.Models.Target."

    Write-Host "[verify] Analysis fixture passed: $($nodes.Count) nodes, $($edges.Count) edges, $($methodEdges.Count) method edges."
}
finally {
    if (-not $KeepTemp) {
        Remove-TempRoot
    } else {
        Write-Host "[verify] Kept temp fixture at $tempRoot"
    }
}
