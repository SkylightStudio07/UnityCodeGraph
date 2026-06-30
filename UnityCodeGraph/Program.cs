using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args.Contains("--help") || args.Contains("-h"))
{
    PrintHelp();
    return 0;
}

var interactive = args.Length == 0;
var targetPath = interactive ? PromptForPath() : Path.GetFullPath(args[0]);
var watch = interactive || HasOption(args, "--watch", "-w");
var rootOption = interactive ? Prompt("Code folder names", "Scripts,Source") : GetOption(args, "--roots");
var scanRootNames = rootOption
    ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
var writeAiContext = !HasOption(args, "--no-ai-context");
var outputPath = interactive
    ? Prompt("Output JSON path", Path.Combine(targetPath, "code-graph.json"))
    : GetOption(args, "--output", "-o") ?? Path.Combine(Environment.CurrentDirectory, "graph.json");

if (!Directory.Exists(targetPath) && !File.Exists(targetPath))
{
    Console.Error.WriteLine($"Path not found: {targetPath}");
    return 1;
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? Environment.CurrentDirectory);
var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

await RunAnalysisAsync(targetPath, outputPath, scanRootNames, jsonOptions, writeAiContext);

if (watch)
{
    await WatchAsync(targetPath, outputPath, scanRootNames, jsonOptions, writeAiContext);
}

return 0;

static string PromptForPath()
{
    Console.WriteLine("UnityCodeGraph watch mode");
    Console.WriteLine("Paste a Unity project path, or press Enter to use the current folder.");
    Console.WriteLine();

    while (true)
    {
        var path = Path.GetFullPath(Prompt("Unity project path", Environment.CurrentDirectory).Trim('"'));
        if (Directory.Exists(path) || File.Exists(path))
        {
            return path;
        }

        Console.WriteLine($"Path not found: {path}");
    }
}

static string Prompt(string label, string defaultValue)
{
    Console.Write($"{label} [{defaultValue}]: ");
    var input = Console.ReadLine();
    return string.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();
}

static async Task RunAnalysisAsync(
    string targetPath,
    string outputPath,
    IReadOnlySet<string>? scanRootNames,
    JsonSerializerOptions jsonOptions,
    bool writeAiContext)
{
    var analyzer = new CodeGraphAnalyzer();
    var graph = analyzer.Analyze(targetPath, scanRootNames);

    await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(graph, jsonOptions));
    string? contextPath = null;
    IReadOnlyList<string> contextMessages = Array.Empty<string>();
    if (writeAiContext)
    {
        var result = await AiContextExporter.WriteAsync(graph, outputPath, jsonOptions);
        contextPath = result.ContextDirectory;
        contextMessages = result.Messages;
    }

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Analyzed {graph.Files.Count} files, {graph.Nodes.Count} types, {graph.Edges.Count} relationships.");
    Console.WriteLine($"Wrote {Path.GetFullPath(outputPath)}");
    if (contextPath is not null)
    {
        Console.WriteLine($"Wrote {contextPath}");
    }
    foreach (var message in contextMessages)
    {
        Console.WriteLine(message);
    }
}

static async Task WatchAsync(
    string targetPath,
    string outputPath,
    IReadOnlySet<string>? scanRootNames,
    JsonSerializerOptions jsonOptions,
    bool writeAiContext)
{
    var watchPath = Directory.Exists(targetPath)
        ? targetPath
        : Path.GetDirectoryName(targetPath) ?? Environment.CurrentDirectory;

    using var changed = new AutoResetEvent(false);
    using var watcher = new FileSystemWatcher(watchPath)
    {
        Filter = "*.cs",
        IncludeSubdirectories = Directory.Exists(targetPath),
        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size
    };

    var stopping = false;
    var lastChange = DateTimeOffset.MinValue;

    FileSystemEventHandler onChanged = (_, eventArgs) =>
    {
        if (!ShouldTriggerWatch(eventArgs.FullPath, scanRootNames))
        {
            return;
        }

        lastChange = DateTimeOffset.UtcNow;
        changed.Set();
    };

    RenamedEventHandler onRenamed = (_, eventArgs) =>
    {
        if (!ShouldTriggerWatch(eventArgs.FullPath, scanRootNames)
            && !ShouldTriggerWatch(eventArgs.OldFullPath, scanRootNames))
        {
            return;
        }

        lastChange = DateTimeOffset.UtcNow;
        changed.Set();
    };

    watcher.Changed += onChanged;
    watcher.Created += onChanged;
    watcher.Deleted += onChanged;
    watcher.Renamed += onRenamed;
    watcher.EnableRaisingEvents = true;

    Console.WriteLine($"Watching {watchPath}");
    Console.WriteLine("Press Ctrl+C to stop.");

    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        stopping = true;
        changed.Set();
    };

    while (!stopping)
    {
        changed.WaitOne();
        if (stopping)
        {
            break;
        }

        await Task.Delay(500);
        while (DateTimeOffset.UtcNow - lastChange < TimeSpan.FromMilliseconds(500))
        {
            await Task.Delay(250);
        }

        try
        {
            await RunAnalysisAsync(targetPath, outputPath, scanRootNames, jsonOptions, writeAiContext);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{DateTime.Now:HH:mm:ss}] Analysis failed: {ex.Message}");
        }
    }
}

static bool ShouldTriggerWatch(string path, IReadOnlySet<string>? scanRootNames)
{
    if (!Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    if (scanRootNames is null || scanRootNames.Count == 0)
    {
        return true;
    }

    var parts = Path.GetFullPath(path)
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Where(part => part.Length > 0);

    return parts.Any(scanRootNames.Contains);
}

static string? GetOption(string[] args, params string[] names)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (names.Contains(args[i], StringComparer.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}

static bool HasOption(string[] args, params string[] names)
{
    return args.Any(arg => names.Contains(arg, StringComparer.OrdinalIgnoreCase));
}

static void PrintHelp()
{
    Console.WriteLine("""
    UnityCodeGraph

    Usage:
      dotnet run --project UnityCodeGraph -- <unity-or-csharp-project-path> --output graph.json
      dotnet run --project UnityCodeGraph -- <unity-project-path> --roots Scripts,Source --output graph.json
      dotnet run --project UnityCodeGraph -- <unity-project-path> --roots Scripts,Source --watch --output graph.json

    Extracts a first-pass C# relationship graph from .cs files:
      - inheritance and interface implementation
      - field, property, event, return, parameter, and local variable type references
      - object creation and typeof references
      - Unity-specific generic calls such as GetComponent<T>() and AddComponent<T>()
      - attributes such as [SerializeField] and [RequireComponent(typeof(...))]

    Options:
      --output, -o <path>   Output JSON path.
      --roots <names>      Only scan directories with these names, such as Scripts,Source.
      --watch, -w          Keep running and regenerate the graph when .cs files change.
      --no-ai-context      Do not write the generated ai-context folder next to the graph JSON.
    """);
}

internal static class AiContextExporter
{
    private const string ManifestFileName = ".unity-code-graph-context.json";
    private const int MaxSystems = 80;
    private const int MaxTypes = 24;
    private const int MaxEntries = 16;
    private const int MaxFlows = 8;
    private const int MaxFlowSteps = 12;
    private const int MaxRelationships = 36;
    private const int MaxMethodCalls = 36;
    private const int MaxEvidence = 16;

    public static async Task<AiContextWriteResult> WriteAsync(CodeGraph graph, string outputPath, JsonSerializerOptions jsonOptions)
    {
        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? Environment.CurrentDirectory;
        var contextDirectory = Path.Combine(outputDirectory, "ai-context");
        var systemsDirectory = Path.Combine(contextDirectory, "systems");
        Directory.CreateDirectory(systemsDirectory);

        CleanPreviousGeneratedFiles(contextDirectory);

        var systems = BuildSystems(graph).ToList();
        var generatedFiles = new List<string>();
        var messages = new List<string>();

        var indexMarkdown = BuildIndexMarkdown(graph, systems);
        await WriteGeneratedFileAsync(contextDirectory, "index.md", indexMarkdown, generatedFiles);

        var indexJson = new
        {
            schemaVersion = 1,
            generator = "Unity Code Graph",
            generatedAt = DateTimeOffset.UtcNow,
            graph = new
            {
                source = graph.RootPath,
                nodeCount = graph.Nodes.Count,
                edgeCount = graph.Edges.Count,
                methodCount = graph.Methods.Count,
                methodEdgeCount = graph.MethodEdges.Count,
                systemCount = graph.SystemClusters.Count,
                exportedSystemCount = systems.Count
            },
            systems = systems.Select(system => new
            {
                system.Id,
                system.Name,
                system.Anchor,
                system.Stats,
                system.RoleEstimate,
                system.StartHere,
                system.CoreTypes,
                system.LikelyFlows,
                system.Relationships,
                system.MethodCalls,
                system.Evidence,
                system.SuggestedAiTask
            })
        };
        await WriteGeneratedFileAsync(contextDirectory, "index.json", JsonSerializer.Serialize(indexJson, jsonOptions), generatedFiles);

        foreach (var system in systems)
        {
            var baseName = SafeFileName(system.Name);
            await WriteGeneratedFileAsync(systemsDirectory, $"{baseName}.md", BuildSystemMarkdown(system), generatedFiles, contextDirectory);
            await WriteGeneratedFileAsync(systemsDirectory, $"{baseName}.json", JsonSerializer.Serialize(system, jsonOptions), generatedFiles, contextDirectory);
        }

        var manifest = new
        {
            generator = "Unity Code Graph",
            generatedAt = DateTimeOffset.UtcNow,
            files = generatedFiles.Order(StringComparer.Ordinal).ToList()
        };
        await File.WriteAllTextAsync(
            Path.Combine(contextDirectory, ManifestFileName),
            JsonSerializer.Serialize(manifest, jsonOptions),
            Encoding.UTF8);

        messages.AddRange(await WriteAgentGuidesAsync(outputDirectory));

        return new AiContextWriteResult(contextDirectory, messages);
    }

    private static async Task<IReadOnlyList<string>> WriteAgentGuidesAsync(string outputDirectory)
    {
        var messages = new List<string>();
        foreach (var fileName in new[] { "AGENTS.md", "CLAUDE.md" })
        {
            var path = Path.Combine(outputDirectory, fileName);
            if (File.Exists(path))
            {
                messages.Add($"Skipped {path} because it already exists.");
                continue;
            }

            await File.WriteAllTextAsync(path, AgentGuideMarkdown(fileName), Encoding.UTF8);
            messages.Add($"Wrote {path}");
        }

        return messages;
    }

    private static string AgentGuideMarkdown(string fileName)
    {
        var title = fileName.Equals("CLAUDE.md", StringComparison.OrdinalIgnoreCase)
            ? "Claude Code Guide"
            : "Project Agent Guide";
        return $$"""
        # {{title}}

        Before answering architecture, code-flow, or feature ownership questions, read:

        - `ai-context/index.md`

        For feature-specific work, also read the matching file under:

        - `ai-context/systems/*.md`

        Treat `ai-context` as generated graph evidence, not as final truth. If behavior matters, inspect the referenced source files and methods directly.

        If `ai-context-enhanced` exists, treat it as AI-written interpretation layered on top of `ai-context`. Prefer `ai-context` for evidence and use enhanced files only as reading guides.

        When graph data looks stale, regenerate the graph before relying on `ai-context`.
        """;
    }

    private static IEnumerable<SystemContext> BuildSystems(CodeGraph graph)
    {
        var nodeById = graph.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var methodById = graph.Methods.ToDictionary(method => method.Id, StringComparer.Ordinal);

        foreach (var cluster in graph.SystemClusters
            .OrderByDescending(cluster => cluster.NodeIds.Count)
            .ThenByDescending(cluster => cluster.InternalEdges)
            .ThenBy(cluster => cluster.Name, StringComparer.Ordinal)
            .Take(MaxSystems))
        {
            var clusterIds = cluster.NodeIds.ToHashSet(StringComparer.Ordinal);
            var clusterNodes = cluster.NodeIds
                .Select(id => nodeById.GetValueOrDefault(id))
                .Where(node => node is not null)
                .Cast<GraphNode>()
                .ToList();
            var degree = BuildDegree(clusterNodes, graph.Edges);
            var coreTypes = clusterNodes
                .Select(node => new TypeSummary(
                    node.Id,
                    node.Name,
                    node.Kind,
                    node.Namespace,
                    node.File,
                    node.Line,
                    node.IsUnityType,
                    degree.TryGetValue(node.Id, out var stat) ? stat.In : 0,
                    degree.TryGetValue(node.Id, out stat) ? stat.Out : 0,
                    degree.TryGetValue(node.Id, out stat) ? stat.Internal : 0,
                    degree.TryGetValue(node.Id, out stat) ? stat.External : 0))
                .OrderByDescending(item => item.Internal + item.External + item.Incoming + item.Outgoing)
                .ThenBy(item => item.Name, StringComparer.Ordinal)
                .Take(MaxTypes)
                .ToList();

            var entries = cluster.EntryMethodIds
                .Select(id => methodById.GetValueOrDefault(id))
                .Where(method => method is not null)
                .Cast<GraphMethod>()
                .OrderBy(method => EntryRank(method))
                .ThenBy(method => method.Line)
                .Take(MaxEntries)
                .Select(method => MethodSummary.From(method))
                .ToList();

            var internalRelationships = graph.Edges
                .Where(edge => clusterIds.Contains(edge.Source) && clusterIds.Contains(edge.Target))
                .OrderByDescending(edge => edge.Weight)
                .ThenBy(edge => edge.Kind, StringComparer.Ordinal)
                .ThenBy(edge => edge.Source, StringComparer.Ordinal)
                .Take(MaxRelationships)
                .Select(edge => RelationshipSummary.From(edge, nodeById, "internal"))
                .ToList();

            var externalRelationships = graph.Edges
                .Where(edge => clusterIds.Contains(edge.Source) != clusterIds.Contains(edge.Target))
                .OrderByDescending(edge => edge.Weight)
                .ThenBy(edge => edge.Kind, StringComparer.Ordinal)
                .ThenBy(edge => edge.Source, StringComparer.Ordinal)
                .Take(MaxRelationships)
                .Select(edge => RelationshipSummary.From(edge, nodeById, clusterIds.Contains(edge.Source) ? "outgoing" : "incoming"))
                .ToList();

            var internalCalls = graph.MethodEdges
                .Select(edge => new { Edge = edge, Source = methodById.GetValueOrDefault(edge.Source), Target = methodById.GetValueOrDefault(edge.Target) })
                .Where(item => item.Source is not null && item.Target is not null && clusterIds.Contains(item.Source.TypeId) && clusterIds.Contains(item.Target.TypeId))
                .OrderByDescending(item => item.Edge.Weight)
                .ThenBy(item => item.Source!.Line)
                .Take(MaxMethodCalls)
                .Select(item => MethodCallSummary.From(item.Edge, item.Source!, item.Target!))
                .ToList();

            var flows = BuildFlows(cluster.EntryMethodIds, graph.MethodEdges, methodById, clusterIds);
            var evidence = BuildEvidence(flows, internalCalls, externalRelationships, internalRelationships, cluster).Take(MaxEvidence).ToList();
            var role = EstimateRole(cluster, clusterNodes);

            yield return new SystemContext(
                cluster.Id,
                cluster.Name,
                $"systems/{SafeFileName(cluster.Name)}.md",
                new SystemStats(cluster.NodeIds.Count, cluster.InternalEdges, cluster.ExternalEdges, cluster.EntryMethodIds.Count, cluster.Keywords),
                role,
                entries,
                coreTypes,
                flows,
                new RelationshipGroups(internalRelationships, externalRelationships),
                new MethodCallGroups(internalCalls),
                evidence,
                $"Use the {cluster.Name} context to explain the reading order, likely runtime flow, and risky assumptions. Cite method names, relationship edges, and file references when possible.");
        }
    }

    private static Dictionary<string, DegreeStat> BuildDegree(IEnumerable<GraphNode> nodes, IEnumerable<GraphEdge> edges)
    {
        var ids = nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var degree = ids.ToDictionary(id => id, _ => new DegreeStat(), StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            var sourceIn = ids.Contains(edge.Source);
            var targetIn = ids.Contains(edge.Target);
            if (!sourceIn && !targetIn)
            {
                continue;
            }

            if (sourceIn && degree.TryGetValue(edge.Source, out var source))
            {
                source.Out += edge.Weight;
                if (targetIn) source.Internal += edge.Weight;
                else source.External += edge.Weight;
            }

            if (targetIn && degree.TryGetValue(edge.Target, out var target))
            {
                target.In += edge.Weight;
                if (sourceIn) target.Internal += edge.Weight;
                else target.External += edge.Weight;
            }
        }

        return degree;
    }

    private static List<FlowSummary> BuildFlows(
        IEnumerable<string> entryMethodIds,
        IEnumerable<GraphMethodEdge> methodEdges,
        IReadOnlyDictionary<string, GraphMethod> methodById,
        IReadOnlySet<string> clusterIds)
    {
        var outgoing = methodEdges
            .Select(edge => new { Edge = edge, Source = methodById.GetValueOrDefault(edge.Source), Target = methodById.GetValueOrDefault(edge.Target) })
            .Where(item => item.Source is not null && item.Target is not null && clusterIds.Contains(item.Source.TypeId) && clusterIds.Contains(item.Target.TypeId))
            .GroupBy(item => item.Edge.Source)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.Edge.Weight).ThenBy(item => item.Target!.Line).ToList(), StringComparer.Ordinal);

        var flows = new List<FlowSummary>();
        foreach (var entry in entryMethodIds.Select(id => methodById.GetValueOrDefault(id)).Where(method => method is not null).Cast<GraphMethod>().Take(MaxFlows))
        {
            var steps = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var current = entry;
            for (var depth = 0; depth < MaxFlowSteps; depth++)
            {
                if (!seen.Add(current.Id))
                {
                    steps.Add($"{MethodLabel(current)} / cycle");
                    break;
                }

                var nextEdges = outgoing.GetValueOrDefault(current.Id);
                steps.Add($"{MethodLabel(current)}{(nextEdges is { Count: > 0 } ? "" : " / terminal")}");
                if (nextEdges is not { Count: > 0 })
                {
                    break;
                }

                current = nextEdges[0].Target!;
            }

            if (steps.Count > 0)
            {
                flows.Add(new FlowSummary(MethodLabel(entry), steps));
            }
        }

        return flows;
    }

    private static IEnumerable<EvidenceSummary> BuildEvidence(
        IReadOnlyList<FlowSummary> flows,
        IReadOnlyList<MethodCallSummary> calls,
        IReadOnlyList<RelationshipSummary> externalRelationships,
        IReadOnlyList<RelationshipSummary> internalRelationships,
        GraphSystemCluster cluster)
    {
        foreach (var flow in flows.Take(2))
        {
            yield return new EvidenceSummary("Likely flow", $"{flow.Entry} -> {string.Join(" -> ", flow.Steps.Skip(1).Take(3))}", null);
        }

        foreach (var call in calls.Take(3))
        {
            yield return new EvidenceSummary("Internal call", $"{call.SourceType}.{call.Source} -> {call.TargetType}.{call.Target}", call.Example);
        }

        foreach (var relationship in externalRelationships.Take(3))
        {
            yield return new EvidenceSummary($"{relationship.Direction} {relationship.Kind}", $"{relationship.SourceName} -> {relationship.TargetName} / {relationship.Weight} refs", relationship.Example);
        }

        foreach (var relationship in internalRelationships)
        {
            yield return new EvidenceSummary($"Internal {relationship.Kind}", $"{relationship.SourceName} -> {relationship.TargetName} / {relationship.Weight} refs", relationship.Example);
        }

        if (!flows.Any() && !calls.Any() && !externalRelationships.Any() && !internalRelationships.Any())
        {
            yield return new EvidenceSummary("System cluster", $"{cluster.Name} contains {cluster.NodeIds.Count} related types.", null);
        }
    }

    private static string BuildIndexMarkdown(CodeGraph graph, IReadOnlyList<SystemContext> systems)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Unity Code Graph AI Context");
        builder.AppendLine();
        builder.AppendLine("This folder is generated from local graph analysis. It is designed for AI coding tools such as Claude Code, Codex, Cursor, and similar agents.");
        builder.AppendLine();
        builder.AppendLine("## How To Use");
        builder.AppendLine();
        builder.AppendLine("- Start with this `index.md` file for a project map.");
        builder.AppendLine("- Open a file under `systems/` when you want focused context for one feature area.");
        builder.AppendLine("- Treat relationships and evidence as extracted facts, but treat role estimates and likely flows as heuristics.");
        builder.AppendLine("- Ask for source files when this context is not enough.");
        builder.AppendLine();
        builder.AppendLine("## Graph Summary");
        builder.AppendLine();
        builder.AppendLine($"- Source: `{EscapeInline(graph.RootPath)}`");
        builder.AppendLine($"- Types: {graph.Nodes.Count}");
        builder.AppendLine($"- Relationships: {graph.Edges.Count}");
        builder.AppendLine($"- Methods: {graph.Methods.Count}");
        builder.AppendLine($"- Method calls: {graph.MethodEdges.Count}");
        builder.AppendLine($"- Systems: {systems.Count}");
        builder.AppendLine($"- Generated: {DateTimeOffset.UtcNow:O}");
        builder.AppendLine();
        builder.AppendLine("## System Index");
        builder.AppendLine();
        builder.AppendLine("| System | Types | Internal | External | Entry Candidates | Context |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: | --- |");
        foreach (var system in systems)
        {
            builder.AppendLine($"| {EscapeTable(system.Name)} | {system.Stats.NodeCount} | {system.Stats.InternalEdges} | {system.Stats.ExternalEdges} | {system.Stats.EntryMethodCount} | [{EscapeTable(system.Anchor)}]({system.Anchor}) |");
        }

        builder.AppendLine();
        return builder.ToString();
    }

    private static string BuildSystemMarkdown(SystemContext system)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {system.Name}");
        builder.AppendLine();
        builder.AppendLine(system.RoleEstimate);
        builder.AppendLine();
        builder.AppendLine("## Stats");
        builder.AppendLine();
        builder.AppendLine($"- Types: {system.Stats.NodeCount}");
        builder.AppendLine($"- Internal relationships: {system.Stats.InternalEdges}");
        builder.AppendLine($"- External relationships: {system.Stats.ExternalEdges}");
        builder.AppendLine($"- Entry candidates: {system.Stats.EntryMethodCount}");
        builder.AppendLine($"- Keywords: {(system.Stats.Keywords.Count > 0 ? string.Join(", ", system.Stats.Keywords.Select(keyword => $"`{EscapeInline(keyword)}`")) : "none")}");
        builder.AppendLine();

        AppendList(builder, "Start Here", system.StartHere, method => $"`{EscapeInline(method.Label)}` - {method.EntryKind} / {method.File}:{method.Line}");
        AppendList(builder, "Core Types", system.CoreTypes, type => $"`{EscapeInline(type.Name)}` - {type.Kind}{(type.IsUnityType ? " / Unity" : "")} / {type.Outgoing} out / {type.Incoming} in / {type.File}:{type.Line}");
        AppendFlows(builder, system.LikelyFlows);
        AppendRelationships(builder, "Internal Type Relationships", system.Relationships.Internal);
        AppendRelationships(builder, "External Touchpoints", system.Relationships.External);
        AppendMethodCalls(builder, system.MethodCalls.Internal);
        AppendEvidence(builder, system.Evidence);
        builder.AppendLine("## Suggested AI Task");
        builder.AppendLine();
        builder.AppendLine(system.SuggestedAiTask);
        builder.AppendLine();
        return builder.ToString();
    }

    private static void AppendList<T>(StringBuilder builder, string title, IReadOnlyList<T> items, Func<T, string> formatter)
    {
        builder.AppendLine($"## {title}");
        builder.AppendLine();
        if (items.Count == 0)
        {
            builder.AppendLine("- None detected.");
            builder.AppendLine();
            return;
        }

        foreach (var item in items)
        {
            builder.AppendLine($"- {formatter(item)}");
        }
        builder.AppendLine();
    }

    private static void AppendFlows(StringBuilder builder, IReadOnlyList<FlowSummary> flows)
    {
        builder.AppendLine("## Likely Method Flows");
        builder.AppendLine();
        if (flows.Count == 0)
        {
            builder.AppendLine("- No internal method flow detected.");
            builder.AppendLine();
            return;
        }

        foreach (var flow in flows)
        {
            builder.AppendLine($"- `{EscapeInline(flow.Entry)}`");
            foreach (var step in flow.Steps)
            {
                builder.AppendLine($"  - `{EscapeInline(step)}`");
            }
        }
        builder.AppendLine();
    }

    private static void AppendRelationships(StringBuilder builder, string title, IReadOnlyList<RelationshipSummary> relationships)
    {
        builder.AppendLine($"## {title}");
        builder.AppendLine();
        if (relationships.Count == 0)
        {
            builder.AppendLine("- None detected.");
            builder.AppendLine();
            return;
        }

        foreach (var edge in relationships)
        {
            builder.AppendLine($"- `{EscapeInline(edge.SourceName)}` -> `{EscapeInline(edge.TargetName)}` - {edge.Direction} / {edge.Kind} / {edge.Weight} refs");
            if (edge.Example is not null)
            {
                builder.AppendLine($"  - Evidence: `{EscapeInline(ExampleLabel(edge.Example))}`");
            }
        }
        builder.AppendLine();
    }

    private static void AppendMethodCalls(StringBuilder builder, IReadOnlyList<MethodCallSummary> calls)
    {
        builder.AppendLine("## Internal Method Calls");
        builder.AppendLine();
        if (calls.Count == 0)
        {
            builder.AppendLine("- None detected.");
            builder.AppendLine();
            return;
        }

        foreach (var call in calls)
        {
            builder.AppendLine($"- `{EscapeInline(call.SourceType)}.{EscapeInline(call.Source)}` -> `{EscapeInline(call.TargetType)}.{EscapeInline(call.Target)}` / {call.Weight} refs");
            if (call.Example is not null)
            {
                builder.AppendLine($"  - Evidence: `{EscapeInline(ExampleLabel(call.Example))}`");
            }
        }
        builder.AppendLine();
    }

    private static void AppendEvidence(StringBuilder builder, IReadOnlyList<EvidenceSummary> evidence)
    {
        builder.AppendLine("## Evidence");
        builder.AppendLine();
        if (evidence.Count == 0)
        {
            builder.AppendLine("- No evidence rows generated.");
            builder.AppendLine();
            return;
        }

        foreach (var item in evidence)
        {
            builder.AppendLine($"- {item.Title} - {item.Detail}");
            if (item.Example is not null)
            {
                builder.AppendLine($"  - `{EscapeInline(ExampleLabel(item.Example))}`");
            }
        }
        builder.AppendLine();
    }

    private static async Task WriteGeneratedFileAsync(string directory, string relativePath, string content, List<string> generatedFiles, string? rootDirectory = null)
    {
        var root = rootDirectory ?? directory;
        var fullPath = Path.Combine(directory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8);
        generatedFiles.Add(Path.GetRelativePath(root, fullPath).Replace('\\', '/'));
    }

    private static void CleanPreviousGeneratedFiles(string contextDirectory)
    {
        var manifestPath = Path.Combine(contextDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (!document.RootElement.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var file in files.EnumerateArray())
            {
                var relative = file.GetString();
                if (string.IsNullOrWhiteSpace(relative))
                {
                    continue;
                }

                var fullPath = Path.GetFullPath(Path.Combine(contextDirectory, relative));
                if (!fullPath.StartsWith(Path.GetFullPath(contextDirectory), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
        }
        catch
        {
            // A broken manifest should not block graph generation.
        }
    }

    private static string EstimateRole(GraphSystemCluster cluster, IReadOnlyList<GraphNode> nodes)
    {
        var unityCount = nodes.Count(node => node.IsUnityType);
        var density = cluster.InternalEdges > cluster.ExternalEdges ? "internally dense" : "externally connected";
        var keywords = cluster.Keywords.Count > 0 ? string.Join(", ", cluster.Keywords.Take(5)) : "shared code";
        return $"{cluster.Name} appears to be an {density} area around {keywords}. It contains {cluster.NodeIds.Count} types, including {unityCount} Unity-facing types.";
    }

    private static int EntryRank(GraphMethod method)
    {
        if (method.EntryKind.Contains("unity", StringComparison.OrdinalIgnoreCase)) return 0;
        if (method.IsEntryPoint) return 1;
        return 2;
    }

    private static string MethodLabel(GraphMethod method)
    {
        return $"{ShortTypeId(method.TypeId)}.{method.Signature}";
    }

    private static string ShortTypeId(string typeId)
    {
        return typeId.Split('.').LastOrDefault()?.Split('+').LastOrDefault() ?? typeId;
    }

    private static string SafeFileName(string value)
    {
        var builder = new StringBuilder();
        foreach (var ch in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-')
            {
                builder.Append(ch);
            }
            else if (char.IsWhiteSpace(ch) || ch is '/' or '\\')
            {
                builder.Append('-');
            }
        }

        var result = builder.ToString().Trim('-');
        while (result.Contains("--", StringComparison.Ordinal))
        {
            result = result.Replace("--", "-", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(result) ? "system" : result[..Math.Min(result.Length, 80)];
    }

    private static string EscapeInline(string value)
    {
        return value.Replace("`", "\\`", StringComparison.Ordinal).ReplaceLineEndings(" ");
    }

    private static string EscapeTable(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");
    }

    private static string ExampleLabel(ExampleSummary example)
    {
        return $"{example.File}:{example.Line} / {example.Text}";
    }

    private sealed class DegreeStat
    {
        public int In { get; set; }
        public int Out { get; set; }
        public int Internal { get; set; }
        public int External { get; set; }
    }

    internal sealed record AiContextWriteResult(string ContextDirectory, IReadOnlyList<string> Messages);

    private sealed record SystemContext(
        string Id,
        string Name,
        string Anchor,
        SystemStats Stats,
        string RoleEstimate,
        IReadOnlyList<MethodSummary> StartHere,
        IReadOnlyList<TypeSummary> CoreTypes,
        IReadOnlyList<FlowSummary> LikelyFlows,
        RelationshipGroups Relationships,
        MethodCallGroups MethodCalls,
        IReadOnlyList<EvidenceSummary> Evidence,
        string SuggestedAiTask);

    private sealed record SystemStats(int NodeCount, int InternalEdges, int ExternalEdges, int EntryMethodCount, IReadOnlyList<string> Keywords);
    private sealed record TypeSummary(string Id, string Name, string Kind, string Namespace, string File, int Line, bool IsUnityType, int Incoming, int Outgoing, int Internal, int External);
    private sealed record FlowSummary(string Entry, IReadOnlyList<string> Steps);
    private sealed record RelationshipGroups(IReadOnlyList<RelationshipSummary> Internal, IReadOnlyList<RelationshipSummary> External);
    private sealed record MethodCallGroups(IReadOnlyList<MethodCallSummary> Internal);
    private sealed record EvidenceSummary(string Title, string Detail, ExampleSummary? Example);

    private sealed record MethodSummary(string Id, string TypeId, string Signature, string Label, string EntryKind, string File, int Line)
    {
        public static MethodSummary From(GraphMethod method)
        {
            return new MethodSummary(method.Id, method.TypeId, method.Signature, MethodLabel(method), method.EntryKind, method.File, method.Line);
        }
    }

    private sealed record RelationshipSummary(string Kind, string Direction, string Source, string SourceName, string Target, string TargetName, int Weight, ExampleSummary? Example)
    {
        public static RelationshipSummary From(GraphEdge edge, IReadOnlyDictionary<string, GraphNode> nodeById, string direction)
        {
            var sourceName = nodeById.TryGetValue(edge.Source, out var source) ? source.Name : ShortTypeId(edge.Source);
            var targetName = nodeById.TryGetValue(edge.Target, out var target) ? target.Name : ShortTypeId(edge.Target);
            return new RelationshipSummary(edge.Kind, direction, edge.Source, sourceName, edge.Target, targetName, edge.Weight, ExampleSummary.From(edge.Examples.FirstOrDefault()));
        }
    }

    private sealed record MethodCallSummary(string SourceType, string Source, string TargetType, string Target, int Weight, ExampleSummary? Example)
    {
        public static MethodCallSummary From(GraphMethodEdge edge, GraphMethod source, GraphMethod target)
        {
            return new MethodCallSummary(ShortTypeId(source.TypeId), source.Signature, ShortTypeId(target.TypeId), target.Signature, edge.Weight, ExampleSummary.From(edge.Examples.FirstOrDefault()));
        }
    }

    private sealed record ExampleSummary(string File, int Line, string Text)
    {
        public static ExampleSummary? From(EdgeExample? example)
        {
            if (example is null)
            {
                return null;
            }

            return new ExampleSummary(example.File, example.Line, example.Text.Length > 240 ? example.Text[..240] : example.Text);
        }
    }
}

internal sealed class CodeGraphAnalyzer
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".idea", "Library", "Temp", "Obj", "obj", "bin", "Build", "Builds", "Logs", "UserSettings"
    };

    private static readonly HashSet<string> PrimitiveTypeNames = new(StringComparer.Ordinal)
    {
        "bool", "byte", "sbyte", "char", "decimal", "double", "float", "int", "uint", "nint", "nuint",
        "long", "ulong", "object", "short", "ushort", "string", "void", "dynamic", "var"
    };

    private static readonly HashSet<string> UnityGenericCalls = new(StringComparer.Ordinal)
    {
        "GetComponent",
        "GetComponents",
        "GetComponentInChildren",
        "GetComponentsInChildren",
        "GetComponentInParent",
        "GetComponentsInParent",
        "TryGetComponent",
        "AddComponent",
        "FindObjectOfType",
        "FindObjectsOfType",
        "FindFirstObjectByType",
        "FindAnyObjectByType",
        "FindObjectsByType",
        "CreateInstance"
    };

    public CodeGraph Analyze(string path, IReadOnlySet<string>? scanRootNames = null)
    {
        var files = CollectFiles(path, scanRootNames).ToList();
        var documents = files.Select(ParseDocument).ToList();
        var nodes = documents.SelectMany(d => d.Types).ToList();
        var resolver = new TypeResolver(nodes);
        var edges = new EdgeCollector(resolver);
        var methods = nodes.SelectMany(MethodNode.FromType).ToList();
        var methodResolver = new MethodResolver(methods);
        var methodEdges = new MethodEdgeCollector();

        foreach (var document in documents)
        {
            foreach (var type in document.Types)
            {
                AnalyzeType(type, edges, methodResolver, methodEdges);
            }
        }

        var graphEdges = edges.Build();
        var graphMethods = methods
            .OrderBy(m => m.TypeId)
            .ThenBy(m => m.Line)
            .Select(m => m.ToDto())
            .ToList();
        var graphMethodEdges = methodEdges.Build();
        var graphNodes = nodes
            .OrderBy(n => n.Namespace)
            .ThenBy(n => n.Name)
            .Select(n => n.ToDto())
            .ToList();

        return new CodeGraph
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            RootPath = path,
            Files = files,
            Nodes = graphNodes,
            Edges = graphEdges,
            Methods = graphMethods,
            MethodEdges = graphMethodEdges,
            SystemClusters = SystemClusterBuilder.Build(graphNodes, graphEdges, graphMethods, graphMethodEdges)
        };
    }

    private static IEnumerable<string> CollectFiles(string path, IReadOnlySet<string>? scanRootNames)
    {
        if (File.Exists(path))
        {
            if (Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                yield return path;
            }

            yield break;
        }

        var searchRoots = FindSearchRoots(path, scanRootNames).ToList();
        foreach (var root in searchRoots)
        {
            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (!ShouldIgnore(file))
                {
                    yield return file;
                }
            }
        }
    }

    private static IEnumerable<string> FindSearchRoots(string path, IReadOnlySet<string>? scanRootNames)
    {
        if (scanRootNames is null || scanRootNames.Count == 0)
        {
            yield return path;
            yield break;
        }

        var fullPath = Path.GetFullPath(path);
        if (scanRootNames.Contains(Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))))
        {
            yield return fullPath;
            yield break;
        }

        foreach (var directory in Directory.EnumerateDirectories(fullPath, "*", SearchOption.AllDirectories))
        {
            if (!ShouldIgnore(directory) && scanRootNames.Contains(Path.GetFileName(directory)))
            {
                yield return directory;
            }
        }
    }

    private static bool ShouldIgnore(string file)
    {
        var parts = Path.GetFullPath(file)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(p => p.Length > 0);

        return parts.Any(part => IgnoredDirectories.Contains(part));
    }

    private static ParsedDocument ParseDocument(string file)
    {
        var source = File.ReadAllText(file);
        var tree = CSharpSyntaxTree.ParseText(source, path: file);
        var root = tree.GetCompilationUnitRoot();

        var types = root.DescendantNodes()
            .OfType<BaseTypeDeclarationSyntax>()
            .Select(type => TypeNode.FromSyntax(type, tree, root))
            .ToList();

        return new ParsedDocument(file, tree, root, types);
    }

    private static void AnalyzeType(
        TypeNode type,
        EdgeCollector edges,
        MethodResolver methodResolver,
        MethodEdgeCollector methodEdges)
    {
        var syntax = type.Syntax;

        if (syntax is TypeDeclarationSyntax typeDeclaration)
        {
            if (typeDeclaration.BaseList is not null)
            {
                var baseTypes = typeDeclaration.BaseList.Types.Select(b => b.Type).ToList();
                for (var i = 0; i < baseTypes.Count; i++)
                {
                    foreach (var reference in TypeReferenceExtractor.Extract(baseTypes[i]))
                    {
                        var kind = edges.IsKnownInterface(reference, type.Id) ? "implements" : i == 0 ? "inherits" : "implements";
                        edges.Add(type.Id, reference, kind, baseTypes[i], type.File);
                    }
                }
            }

            foreach (var constraint in typeDeclaration.ConstraintClauses)
            {
                foreach (var typeConstraint in constraint.Constraints.OfType<TypeConstraintSyntax>())
                {
                    AddTypeEdges(type, edges, typeConstraint.Type, "type_constraint");
                }
            }
        }

        foreach (var attribute in syntax.AttributeLists.SelectMany(list => list.Attributes))
        {
            edges.Add(type.Id, NormalizeAttributeName(attribute.Name), "has_attribute", attribute, type.File);
            foreach (var referencedType in ExtractTypeOfArguments(attribute))
            {
                edges.Add(type.Id, referencedType, "attribute_type_argument", attribute, type.File);
            }
        }

        if (syntax is not TypeDeclarationSyntax typeWithMembers)
        {
            return;
        }

        var fieldVariables = BuildMemberVariableMap(type, typeWithMembers, edges);

        foreach (var member in typeWithMembers.Members)
        {
            AnalyzeMember(type, member, edges, fieldVariables, methodResolver, methodEdges);
        }
    }

    private static Dictionary<string, string> BuildMemberVariableMap(TypeNode owner, TypeDeclarationSyntax type, EdgeCollector edges)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in type.Members.OfType<FieldDeclarationSyntax>())
        {
            var reference = ChooseResolvableReference(field.Declaration.Type, edges, owner.Id);
            if (reference is null)
            {
                continue;
            }

            foreach (var variable in field.Declaration.Variables)
            {
                variables[variable.Identifier.ValueText] = reference;
            }
        }

        foreach (var property in type.Members.OfType<PropertyDeclarationSyntax>())
        {
            var reference = ChooseResolvableReference(property.Type, edges, owner.Id);
            if (reference is not null)
            {
                variables[property.Identifier.ValueText] = reference;
            }
        }

        return variables;
    }

    private static void AnalyzeMember(
        TypeNode owner,
        MemberDeclarationSyntax member,
        EdgeCollector edges,
        IReadOnlyDictionary<string, string> fieldVariables,
        MethodResolver methodResolver,
        MethodEdgeCollector methodEdges)
    {
        var variableTypes = new Dictionary<string, string>(fieldVariables, StringComparer.Ordinal);
        var currentMethod = MethodNode.TryGetId(owner, member);

        foreach (var attribute in member.AttributeLists.SelectMany(list => list.Attributes))
        {
            edges.Add(owner.Id, NormalizeAttributeName(attribute.Name), "member_attribute", attribute, owner.File);
            foreach (var referencedType in ExtractTypeOfArguments(attribute))
            {
                edges.Add(owner.Id, referencedType, "attribute_type_argument", attribute, owner.File);
            }
        }

        switch (member)
        {
            case FieldDeclarationSyntax field:
                AddTypeEdges(owner, edges, field.Declaration.Type, "has_field_type");
                break;
            case PropertyDeclarationSyntax property:
                AddTypeEdges(owner, edges, property.Type, "has_property_type");
                break;
            case EventFieldDeclarationSyntax eventField:
                AddTypeEdges(owner, edges, eventField.Declaration.Type, "has_event_type");
                break;
            case MethodDeclarationSyntax method:
                AddTypeEdges(owner, edges, method.ReturnType, "returns");
                foreach (var parameter in method.ParameterList.Parameters)
                {
                    AddParameterEdges(owner, edges, parameter);
                    AddParameterVariable(owner, variableTypes, parameter, edges);
                }
                AddConstraintEdges(owner, edges, method.ConstraintClauses);
                break;
            case ConstructorDeclarationSyntax constructor:
                foreach (var parameter in constructor.ParameterList.Parameters)
                {
                    AddParameterEdges(owner, edges, parameter);
                    AddParameterVariable(owner, variableTypes, parameter, edges);
                }
                break;
            case OperatorDeclarationSyntax op:
                AddTypeEdges(owner, edges, op.ReturnType, "returns");
                foreach (var parameter in op.ParameterList.Parameters)
                {
                    AddParameterEdges(owner, edges, parameter);
                    AddParameterVariable(owner, variableTypes, parameter, edges);
                }
                break;
            case ConversionOperatorDeclarationSyntax conversion:
                AddTypeEdges(owner, edges, conversion.Type, "returns");
                foreach (var parameter in conversion.ParameterList.Parameters)
                {
                    AddParameterEdges(owner, edges, parameter);
                    AddParameterVariable(owner, variableTypes, parameter, edges);
                }
                break;
        }

        foreach (var local in member.DescendantNodes().OfType<VariableDeclarationSyntax>())
        {
            if (local.Parent is FieldDeclarationSyntax or EventFieldDeclarationSyntax)
            {
                continue;
            }

            AddTypeEdges(owner, edges, local.Type, "uses_local_type");
            AddLocalVariables(owner, variableTypes, local, edges);
        }

        foreach (var foreachStatement in member.DescendantNodes().OfType<ForEachStatementSyntax>())
        {
            AddTypeEdges(owner, edges, foreachStatement.Type, "uses_local_type");
            var reference = ChooseResolvableReference(foreachStatement.Type, edges, owner.Id);
            if (reference is not null)
            {
                variableTypes[foreachStatement.Identifier.ValueText] = reference;
            }
        }

        foreach (var creation in member.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            AddTypeEdges(owner, edges, creation.Type, "creates");
        }

        foreach (var typeOf in member.DescendantNodes().OfType<TypeOfExpressionSyntax>())
        {
            AddTypeEdges(owner, edges, typeOf.Type, "typeof");
        }

        foreach (var cast in member.DescendantNodes().OfType<CastExpressionSyntax>())
        {
            AddTypeEdges(owner, edges, cast.Type, "casts_to");
        }

        foreach (var binary in member.DescendantNodes().OfType<BinaryExpressionSyntax>())
        {
            if ((binary.IsKind(SyntaxKind.AsExpression) || binary.IsKind(SyntaxKind.IsExpression))
                && binary.Right is TypeSyntax typeSyntax)
            {
                AddTypeEdges(owner, edges, typeSyntax, "type_check");
            }
        }

        foreach (var pattern in member.DescendantNodes().OfType<DeclarationPatternSyntax>())
        {
            AddTypeEdges(owner, edges, pattern.Type, "type_check");
            var reference = ChooseResolvableReference(pattern.Type, edges, owner.Id);
            if (reference is not null && pattern.Designation is SingleVariableDesignationSyntax designation)
            {
                variableTypes[designation.Identifier.ValueText] = reference;
            }
        }

        foreach (var assignment in member.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
            {
                continue;
            }

            var name = GetAssignableName(assignment.Left);
            if (name is null)
            {
                continue;
            }

            var reference = InferReferenceFromInitializer(assignment.Right, edges, owner.Id);
            if (reference is not null)
            {
                variableTypes[name] = reference;
            }
        }

        foreach (var invocation in member.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            AnalyzeInvocation(owner, invocation, edges);
            AnalyzeMemberCall(owner, invocation, edges, variableTypes);
            AnalyzeMethodCall(owner, currentMethod, invocation, edges, methodResolver, methodEdges, variableTypes);
        }
    }

    private static void AnalyzeInvocation(TypeNode owner, InvocationExpressionSyntax invocation, EdgeCollector edges)
    {
        var call = GetGenericCall(invocation.Expression);
        if (call is null || !UnityGenericCalls.Contains(call.Value.Name))
        {
            return;
        }

        var edgeKind = call.Value.Name switch
        {
            "AddComponent" => "unity_add_component",
            "CreateInstance" => "unity_create_scriptable_object",
            "TryGetComponent" => "unity_try_get_component",
            var name when name.StartsWith("Find", StringComparison.Ordinal) => "unity_find_object",
            _ => "unity_get_component"
        };

        foreach (var argument in call.Value.TypeArguments)
        {
            AddTypeEdges(owner, edges, argument, edgeKind);
        }
    }

    private static (string Name, IReadOnlyList<TypeSyntax> TypeArguments)? GetGenericCall(ExpressionSyntax expression)
    {
        return expression switch
        {
            GenericNameSyntax generic => (generic.Identifier.ValueText, generic.TypeArgumentList.Arguments),
            MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } => (generic.Identifier.ValueText, generic.TypeArgumentList.Arguments),
            _ => null
        };
    }

    private static void AddParameterEdges(TypeNode owner, EdgeCollector edges, ParameterSyntax parameter)
    {
        if (parameter.Type is not null)
        {
            AddTypeEdges(owner, edges, parameter.Type, "accepts_parameter");
        }
    }

    private static string? GetAssignableName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax
            {
                Expression: ThisExpressionSyntax,
                Name: IdentifierNameSyntax identifier
            } => identifier.Identifier.ValueText,
            _ => null
        };
    }

    private static void AddConstraintEdges(TypeNode owner, EdgeCollector edges, SyntaxList<TypeParameterConstraintClauseSyntax> clauses)
    {
        foreach (var constraint in clauses)
        {
            foreach (var typeConstraint in constraint.Constraints.OfType<TypeConstraintSyntax>())
            {
                AddTypeEdges(owner, edges, typeConstraint.Type, "type_constraint");
            }
        }
    }

    private static void AddParameterVariable(TypeNode owner, Dictionary<string, string> variableTypes, ParameterSyntax parameter, EdgeCollector edges)
    {
        if (parameter.Type is null)
        {
            return;
        }

        var reference = ChooseResolvableReference(parameter.Type, edges, owner.Id);
        if (reference is not null)
        {
            variableTypes[parameter.Identifier.ValueText] = reference;
        }
    }

    private static void AddLocalVariables(TypeNode owner, Dictionary<string, string> variableTypes, VariableDeclarationSyntax local, EdgeCollector edges)
    {
        var explicitReference = ChooseResolvableReference(local.Type, edges, owner.Id);

        foreach (var variable in local.Variables)
        {
            var reference = explicitReference ?? InferReferenceFromInitializer(variable.Initializer?.Value, edges, owner.Id);
            if (reference is not null)
            {
                variableTypes[variable.Identifier.ValueText] = reference;
            }
        }
    }

    private static string? InferReferenceFromInitializer(ExpressionSyntax? initializer, EdgeCollector edges, string sourceId)
    {
        return initializer switch
        {
            ObjectCreationExpressionSyntax objectCreation => ChooseResolvableReference(objectCreation.Type, edges, sourceId),
            CastExpressionSyntax cast => ChooseResolvableReference(cast.Type, edges, sourceId),
            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AsExpression) && binary.Right is TypeSyntax type => ChooseResolvableReference(type, edges, sourceId),
            ParenthesizedExpressionSyntax parenthesized => InferReferenceFromInitializer(parenthesized.Expression, edges, sourceId),
            InvocationExpressionSyntax invocation => InferReferenceFromInvocation(invocation, edges, sourceId),
            _ => null
        };
    }

    private static string? InferReferenceFromInvocation(InvocationExpressionSyntax invocation, EdgeCollector edges, string sourceId)
    {
        var call = GetGenericCall(invocation.Expression);
        if (call is null || call.Value.TypeArguments.Count == 0)
        {
            return null;
        }

        if (!UnityGenericCalls.Contains(call.Value.Name) && call.Value.Name != "OfType")
        {
            return null;
        }

        return call.Value.TypeArguments
            .Select(type => ChooseResolvableReference(type, edges, sourceId))
            .FirstOrDefault(reference => reference is not null);
    }

    private static void AnalyzeMemberCall(
        TypeNode owner,
        InvocationExpressionSyntax invocation,
        EdgeCollector edges,
        IReadOnlyDictionary<string, string> variableTypes)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var targetReference = ResolveReceiverType(memberAccess.Expression, variableTypes)
            ?? ResolveTypeReceiver(memberAccess.Expression, edges, owner.Id);
        if (targetReference is not null)
        {
            edges.Add(owner.Id, targetReference, "calls_member", invocation, owner.File);
        }
    }

    private static void AnalyzeMethodCall(
        TypeNode owner,
        string? currentMethod,
        InvocationExpressionSyntax invocation,
        EdgeCollector edges,
        MethodResolver methodResolver,
        MethodEdgeCollector methodEdges,
        IReadOnlyDictionary<string, string> variableTypes)
    {
        if (currentMethod is null)
        {
            return;
        }

        var target = ResolveMethodTarget(owner, invocation.Expression, edges, methodResolver, variableTypes);
        if (target is null || target == currentMethod)
        {
            return;
        }

        methodEdges.Add(currentMethod, target, "calls", invocation, owner.File);
    }

    private static string? ResolveMethodTarget(
        TypeNode owner,
        ExpressionSyntax expression,
        EdgeCollector edges,
        MethodResolver methodResolver,
        IReadOnlyDictionary<string, string> variableTypes)
    {
        switch (expression)
        {
            case IdentifierNameSyntax identifier:
                return methodResolver.Resolve(owner.Id, identifier.Identifier.ValueText);
            case GenericNameSyntax generic:
                return methodResolver.Resolve(owner.Id, generic.Identifier.ValueText);
            case MemberAccessExpressionSyntax memberAccess:
                var methodName = memberAccess.Name switch
                {
                    IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                    GenericNameSyntax generic => generic.Identifier.ValueText,
                    _ => memberAccess.Name.ToString()
                };

                if (memberAccess.Expression is ThisExpressionSyntax)
                {
                    return methodResolver.Resolve(owner.Id, methodName);
                }

                var targetType = ResolveReceiverType(memberAccess.Expression, variableTypes);
                if (targetType is not null)
                {
                    return methodResolver.Resolve(targetType, methodName);
                }

                var staticTargetType = ResolveTypeReceiver(memberAccess.Expression, edges, owner.Id);
                if (staticTargetType is not null)
                {
                    return methodResolver.Resolve(staticTargetType, methodName);
                }

                return null;
            default:
                return null;
        }
    }

    private static string? ResolveReceiverType(ExpressionSyntax receiver, IReadOnlyDictionary<string, string> variableTypes)
    {
        return receiver switch
        {
            IdentifierNameSyntax identifier when variableTypes.TryGetValue(identifier.Identifier.ValueText, out var targetType) => targetType,
            MemberAccessExpressionSyntax
            {
                Expression: ThisExpressionSyntax,
                Name: IdentifierNameSyntax identifier
            } when variableTypes.TryGetValue(identifier.Identifier.ValueText, out var targetType) => targetType,
            MemberAccessExpressionSyntax
            {
                Expression: ThisExpressionSyntax,
                Name: GenericNameSyntax generic
            } when variableTypes.TryGetValue(generic.Identifier.ValueText, out var targetType) => targetType,
            _ => null
        };
    }

    private static string? ResolveTypeReceiver(ExpressionSyntax receiver, EdgeCollector edges, string sourceId)
    {
        return receiver switch
        {
            IdentifierNameSyntax identifier => edges.ResolveId(identifier.Identifier.ValueText, sourceId),
            MemberAccessExpressionSyntax memberAccess => edges.ResolveId(memberAccess.ToString(), sourceId),
            _ => null
        };
    }

    private static string? ChooseResolvableReference(TypeSyntax type, EdgeCollector edges, string sourceId)
    {
        foreach (var reference in TypeReferenceExtractor.Extract(type))
        {
            var resolved = edges.ResolveId(reference, sourceId);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return null;
    }

    private static void AddTypeEdges(TypeNode owner, EdgeCollector edges, TypeSyntax type, string kind)
    {
        foreach (var reference in TypeReferenceExtractor.Extract(type))
        {
            edges.Add(owner.Id, reference, kind, type, owner.File);
        }
    }

    private static string NormalizeAttributeName(NameSyntax name)
    {
        var text = name switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.ValueText,
            _ => name.ToString()
        };

        return text.EndsWith("Attribute", StringComparison.Ordinal) ? text[..^"Attribute".Length] : text;
    }

    private static IEnumerable<string> ExtractTypeOfArguments(AttributeSyntax attribute)
    {
        return attribute.ArgumentList?.Arguments
            .SelectMany(argument => argument.Expression.DescendantNodesAndSelf().OfType<TypeOfExpressionSyntax>())
            .SelectMany(typeOf => TypeReferenceExtractor.Extract(typeOf.Type))
            ?? Enumerable.Empty<string>();
    }

    internal static bool IsPrimitive(string typeName)
    {
        return PrimitiveTypeNames.Contains(typeName);
    }
}

internal sealed record ParsedDocument(
    string File,
    SyntaxTree Tree,
    CompilationUnitSyntax Root,
    IReadOnlyList<TypeNode> Types);

internal sealed class TypeNode
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public required string Kind { get; init; }
    public required string File { get; init; }
    public required int Line { get; init; }
    public required List<string> Usings { get; init; }
    public required BaseTypeDeclarationSyntax Syntax { get; init; }

    public static TypeNode FromSyntax(BaseTypeDeclarationSyntax syntax, SyntaxTree tree, CompilationUnitSyntax root)
    {
        var @namespace = GetNamespace(syntax);
        var name = GetNestedTypeName(syntax);
        var kind = syntax switch
        {
            ClassDeclarationSyntax => "class",
            InterfaceDeclarationSyntax => "interface",
            StructDeclarationSyntax => "struct",
            RecordDeclarationSyntax record when record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) => "record_struct",
            RecordDeclarationSyntax => "record",
            EnumDeclarationSyntax => "enum",
            _ => "type"
        };

        var lineSpan = tree.GetLineSpan(syntax.Identifier.Span);
        var fullName = string.IsNullOrWhiteSpace(@namespace) ? name : $"{@namespace}.{name}";

        return new TypeNode
        {
            Id = fullName,
            Name = name,
            Namespace = @namespace,
            Kind = kind,
            File = tree.FilePath,
            Line = lineSpan.StartLinePosition.Line + 1,
            Usings = GetUsings(syntax, root),
            Syntax = syntax
        };
    }

    public GraphNode ToDto()
    {
        var baseNames = Syntax is TypeDeclarationSyntax typeDeclaration && typeDeclaration.BaseList is not null
            ? typeDeclaration.BaseList.Types.Select(t => t.Type.ToString()).ToList()
            : new List<string>();

        var attributes = Syntax.AttributeLists
            .SelectMany(list => list.Attributes)
            .Select(attribute => attribute.Name.ToString())
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        return new GraphNode
        {
            Id = Id,
            Name = Name,
            Namespace = Namespace,
            Kind = Kind,
            File = File,
            Line = Line,
            BaseTypes = baseNames,
            Attributes = attributes,
            IsUnityType = baseNames.Any(IsUnityBaseName)
        };
    }

    private static bool IsUnityBaseName(string name)
    {
        return name.EndsWith("MonoBehaviour", StringComparison.Ordinal)
            || name.EndsWith("ScriptableObject", StringComparison.Ordinal)
            || name.EndsWith("Editor", StringComparison.Ordinal)
            || name.EndsWith("EditorWindow", StringComparison.Ordinal);
    }

    private static string GetNamespace(SyntaxNode syntax)
    {
        var namespaces = syntax.Ancestors()
            .Where(a => a is NamespaceDeclarationSyntax or FileScopedNamespaceDeclarationSyntax)
            .Select(a => a switch
            {
                NamespaceDeclarationSyntax namespaceDeclaration => namespaceDeclaration.Name.ToString(),
                FileScopedNamespaceDeclarationSyntax fileScopedNamespace => fileScopedNamespace.Name.ToString(),
                _ => string.Empty
            })
            .Reverse()
            .Where(x => !string.IsNullOrWhiteSpace(x));

        return string.Join(".", namespaces);
    }

    private static string GetNestedTypeName(BaseTypeDeclarationSyntax syntax)
    {
        var names = syntax.AncestorsAndSelf()
            .OfType<BaseTypeDeclarationSyntax>()
            .Reverse()
            .Select(t => t.Identifier.ValueText);

        return string.Join("+", names);
    }

    private static List<string> GetUsings(BaseTypeDeclarationSyntax syntax, CompilationUnitSyntax root)
    {
        var fileUsings = root.Usings
            .Where(u => u.Alias is null && !u.StaticKeyword.IsKind(SyntaxKind.StaticKeyword))
            .Select(u => u.Name?.ToString())
            .Where(name => !string.IsNullOrWhiteSpace(name));

        var namespaceUsings = syntax.Ancestors()
            .Where(a => a is NamespaceDeclarationSyntax or FileScopedNamespaceDeclarationSyntax)
            .Reverse()
            .SelectMany(a => a switch
            {
                NamespaceDeclarationSyntax namespaceDeclaration => namespaceDeclaration.Usings,
                FileScopedNamespaceDeclarationSyntax fileScopedNamespace => fileScopedNamespace.Usings,
                _ => Enumerable.Empty<UsingDirectiveSyntax>()
            })
            .Where(u => u is not null && u.Alias is null && !u.StaticKeyword.IsKind(SyntaxKind.StaticKeyword))
            .Select(u => u!.Name?.ToString())
            .Where(name => !string.IsNullOrWhiteSpace(name));

        return fileUsings
            .Concat(namespaceUsings)
            .Distinct(StringComparer.Ordinal)
            .Cast<string>()
            .ToList();
    }
}

internal sealed class TypeResolver
{
    private readonly Dictionary<string, List<TypeNode>> _bySimpleName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TypeNode> _byFullName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TypeNode> _contexts = new(StringComparer.Ordinal);

    public TypeResolver(IEnumerable<TypeNode> nodes)
    {
        foreach (var node in nodes)
        {
            _byFullName[node.Id] = node;
            _contexts[node.Id] = node;
            AddSimple(node.Name.Split('+').Last(), node);
            AddSimple(node.Name, node);
        }
    }

    public TypeNode? Resolve(string reference, string? sourceId = null)
    {
        reference = CleanReference(reference);
        if (reference.Length == 0 || CodeGraphAnalyzer.IsPrimitive(reference))
        {
            return null;
        }

        if (_byFullName.TryGetValue(reference, out var exact))
        {
            return exact;
        }

        if (sourceId is not null && _contexts.TryGetValue(sourceId, out var context))
        {
            var contextual = ResolveFromContext(reference, context);
            if (contextual is not null)
            {
                return contextual;
            }
        }

        var simpleName = reference.Split('.').Last().Split('+').Last();
        if (_bySimpleName.TryGetValue(simpleName, out var candidates) && candidates.Count == 1)
        {
            return candidates[0];
        }

        return null;
    }

    public bool IsKnownInterface(string reference)
    {
        return Resolve(reference)?.Kind == "interface";
    }

    public bool IsKnownInterface(string reference, string sourceId)
    {
        return Resolve(reference, sourceId)?.Kind == "interface";
    }

    private void AddSimple(string name, TypeNode node)
    {
        if (!_bySimpleName.TryGetValue(name, out var list))
        {
            list = new List<TypeNode>();
            _bySimpleName[name] = list;
        }

        if (!list.Any(existing => existing.Id == node.Id))
        {
            list.Add(node);
        }
    }

    private TypeNode? ResolveFromContext(string reference, TypeNode context)
    {
        foreach (var candidate in CandidateQualifiedNames(reference, context))
        {
            if (_byFullName.TryGetValue(candidate, out var exact))
            {
                return exact;
            }

            var nestedCandidate = ToNestedTypeName(candidate);
            if (!string.Equals(nestedCandidate, candidate, StringComparison.Ordinal)
                && _byFullName.TryGetValue(nestedCandidate, out var nested))
            {
                return nested;
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateQualifiedNames(string reference, TypeNode context)
    {
        foreach (var namespaceCandidate in NamespaceSearchOrder(context.Namespace))
        {
            yield return string.IsNullOrWhiteSpace(namespaceCandidate)
                ? reference
                : $"{namespaceCandidate}.{reference}";
        }

        foreach (var import in context.Usings)
        {
            yield return $"{import}.{reference}";
        }
    }

    private static IEnumerable<string> NamespaceSearchOrder(string @namespace)
    {
        if (string.IsNullOrWhiteSpace(@namespace))
        {
            yield return string.Empty;
            yield break;
        }

        var current = @namespace;
        while (current.Length > 0)
        {
            yield return current;
            var lastDot = current.LastIndexOf('.');
            if (lastDot < 0)
            {
                break;
            }

            current = current[..lastDot];
        }

        yield return string.Empty;
    }

    private static string ToNestedTypeName(string reference)
    {
        var lastDot = reference.LastIndexOf('.');
        if (lastDot < 0)
        {
            return reference;
        }

        var prefix = reference[..lastDot];
        var suffix = reference[(lastDot + 1)..];
        var prefixLastDot = prefix.LastIndexOf('.');
        if (prefixLastDot < 0)
        {
            return $"{prefix}+{suffix}";
        }

        return $"{prefix[..prefixLastDot]}.{prefix[(prefixLastDot + 1)..]}+{suffix}";
    }

    private static string CleanReference(string reference)
    {
        return reference.Trim()
            .TrimEnd('?')
            .Replace("global::", "", StringComparison.Ordinal);
    }
}

internal sealed class MethodNode
{
    private static readonly HashSet<string> UnityEntryNames = new(StringComparer.Ordinal)
    {
        "Awake", "Start", "OnEnable", "OnDisable", "OnDestroy", "Update", "FixedUpdate", "LateUpdate",
        "OnValidate", "Reset", "OnSceneLoaded"
    };

    private static readonly string[] FlowEntryPrefixes =
    [
        "Init", "Initialize", "Generate", "Build", "Load", "Create", "Spawn", "Setup", "Run", "Begin", "Start"
    ];

    public required string Id { get; init; }
    public required string TypeId { get; init; }
    public required string Name { get; init; }
    public required string Signature { get; init; }
    public required string Kind { get; init; }
    public required string File { get; init; }
    public required int Line { get; init; }
    public required bool IsEntryPoint { get; init; }
    public required string EntryKind { get; init; }

    public static IEnumerable<MethodNode> FromType(TypeNode type)
    {
        if (type.Syntax is not TypeDeclarationSyntax declaration)
        {
            yield break;
        }

        foreach (var member in declaration.Members)
        {
            var node = FromMember(type, member);
            if (node is not null)
            {
                yield return node;
            }
        }
    }

    public static string? TryGetId(TypeNode type, MemberDeclarationSyntax member)
    {
        return FromMember(type, member)?.Id;
    }

    public GraphMethod ToDto()
    {
        return new GraphMethod
        {
            Id = Id,
            TypeId = TypeId,
            Name = Name,
            Signature = Signature,
            Kind = Kind,
            File = File,
            Line = Line,
            IsEntryPoint = IsEntryPoint,
            EntryKind = EntryKind
        };
    }

    private static MethodNode? FromMember(TypeNode type, MemberDeclarationSyntax member)
    {
        var (name, signature, kind, identifierSpan) = member switch
        {
            MethodDeclarationSyntax method => (
                method.Identifier.ValueText,
                $"{method.Identifier.ValueText}({string.Join(", ", method.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? "var"))})",
                "method",
                method.Identifier.Span),
            ConstructorDeclarationSyntax constructor => (
                ".ctor",
                $"{constructor.Identifier.ValueText}({string.Join(", ", constructor.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? "var"))})",
                "constructor",
                constructor.Identifier.Span),
            _ => default
        };

        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var lineSpan = member.SyntaxTree.GetLineSpan(identifierSpan);
        var entryKind = GetEntryKind(name);
        var id = $"{type.Id}.{name}@{lineSpan.StartLinePosition.Line + 1}";

        return new MethodNode
        {
            Id = id,
            TypeId = type.Id,
            Name = name,
            Signature = signature,
            Kind = kind,
            File = type.File,
            Line = lineSpan.StartLinePosition.Line + 1,
            IsEntryPoint = entryKind.Length > 0,
            EntryKind = entryKind
        };
    }

    private static string GetEntryKind(string name)
    {
        if (UnityEntryNames.Contains(name))
        {
            return "unity_lifecycle";
        }

        if (FlowEntryPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return "flow_candidate";
        }

        return string.Empty;
    }
}

internal sealed class MethodResolver(IEnumerable<MethodNode> methods)
{
    private readonly Dictionary<string, List<MethodNode>> _byTypeAndName = methods
        .GroupBy(m => $"{m.TypeId}|{m.Name}", StringComparer.Ordinal)
        .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

    public string? Resolve(string typeReference, string methodName)
    {
        var exactKey = $"{typeReference}|{methodName}";
        if (_byTypeAndName.TryGetValue(exactKey, out var exact) && exact.Count > 0)
        {
            return exact.OrderBy(m => m.Line).First().Id;
        }

        var simpleType = typeReference.Split('.').Last().Split('+').Last();
        var candidate = _byTypeAndName
            .Where(pair => pair.Key.EndsWith($".{simpleType}|{methodName}", StringComparison.Ordinal)
                || pair.Key.EndsWith($"+{simpleType}|{methodName}", StringComparison.Ordinal)
                || pair.Key.StartsWith($"{simpleType}|{methodName}", StringComparison.Ordinal))
            .SelectMany(pair => pair.Value)
            .OrderBy(m => m.Line)
            .FirstOrDefault();

        return candidate?.Id;
    }
}

internal sealed class MethodEdgeCollector
{
    private readonly Dictionary<string, GraphMethodEdge> _edges = new(StringComparer.Ordinal);

    public void Add(string sourceId, string targetId, string kind, SyntaxNode syntax, string file)
    {
        var location = syntax.SyntaxTree.GetLineSpan(syntax.Span);
        var key = $"{sourceId}|{targetId}|{kind}";
        if (!_edges.TryGetValue(key, out var edge))
        {
            edge = new GraphMethodEdge
            {
                Source = sourceId,
                Target = targetId,
                Kind = kind,
                Weight = 0,
                Examples = new List<EdgeExample>()
            };
            _edges[key] = edge;
        }

        edge.Weight++;
        if (edge.Examples.Count < 5)
        {
            edge.Examples.Add(new EdgeExample
            {
                File = file,
                Line = location.StartLinePosition.Line + 1,
                Text = syntax.ToString()
            });
        }
    }

    public List<GraphMethodEdge> Build()
    {
        return _edges.Values
            .OrderBy(e => e.Source)
            .ThenBy(e => e.Target)
            .ToList();
    }
}

internal sealed class EdgeCollector(TypeResolver resolver)
{
    private readonly Dictionary<string, GraphEdge> _edges = new(StringComparer.Ordinal);

    public bool IsKnownInterface(string reference)
    {
        return resolver.IsKnownInterface(reference);
    }

    public bool IsKnownInterface(string reference, string sourceId)
    {
        return resolver.IsKnownInterface(reference, sourceId);
    }

    public bool CanResolve(string reference, string sourceId)
    {
        return resolver.Resolve(reference, sourceId) is not null;
    }

    public string? ResolveId(string reference, string sourceId)
    {
        return resolver.Resolve(reference, sourceId)?.Id;
    }

    public void Add(string sourceId, string targetReference, string kind, SyntaxNode syntax, string file)
    {
        var target = resolver.Resolve(targetReference, sourceId);
        if (target is null || target.Id == sourceId)
        {
            return;
        }

        var location = syntax.SyntaxTree.GetLineSpan(syntax.Span);
        var key = $"{sourceId}|{target.Id}|{kind}";
        if (!_edges.TryGetValue(key, out var edge))
        {
            edge = new GraphEdge
            {
                Source = sourceId,
                Target = target.Id,
                Kind = kind,
                Weight = 0,
                Examples = new List<EdgeExample>()
            };
            _edges[key] = edge;
        }

        edge.Weight++;
        if (edge.Examples.Count < 5)
        {
            edge.Examples.Add(new EdgeExample
            {
                File = file,
                Line = location.StartLinePosition.Line + 1,
                Text = syntax.ToString()
            });
        }
    }

    public List<GraphEdge> Build()
    {
        return _edges.Values
            .OrderBy(e => e.Source)
            .ThenBy(e => e.Kind)
            .ThenBy(e => e.Target)
            .ToList();
    }
}

internal static class SystemClusterBuilder
{
    private static readonly Dictionary<string, (string Key, string Name)> DomainTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["battle"] = ("battle", "Battle System"),
        ["combat"] = ("battle", "Battle System"),
        ["enemy"] = ("battle", "Battle System"),
        ["turn"] = ("battle", "Battle System"),
        ["phase"] = ("battle", "Battle System"),
        ["damage"] = ("battle", "Battle System"),
        ["ammo"] = ("battle", "Battle System"),
        ["status"] = ("battle", "Battle System"),
        ["card"] = ("card", "Card System"),
        ["deck"] = ("card", "Card System"),
        ["hand"] = ("card", "Card System"),
        ["pile"] = ("card", "Card System"),
        ["rarity"] = ("card", "Card System"),
        ["reward"] = ("card", "Card System"),
        ["map"] = ("map", "Map Generation"),
        ["room"] = ("map", "Map Generation"),
        ["node"] = ("map", "Map Generation"),
        ["tile"] = ("map", "Map Generation"),
        ["path"] = ("map", "Map Generation"),
        ["generate"] = ("map", "Map Generation"),
        ["procedural"] = ("map", "Map Generation"),
        ["ui"] = ("ui", "UI Layer"),
        ["view"] = ("ui", "UI Layer"),
        ["panel"] = ("ui", "UI Layer"),
        ["button"] = ("ui", "UI Layer"),
        ["screen"] = ("ui", "UI Layer"),
        ["hud"] = ("ui", "UI Layer"),
        ["menu"] = ("ui", "UI Layer"),
        ["event"] = ("action", "Action Event Pipeline"),
        ["choice"] = ("action", "Action Event Pipeline"),
        ["effect"] = ("action", "Action Event Pipeline"),
        ["action"] = ("action", "Action Event Pipeline"),
        ["save"] = ("data", "Data / Config"),
        ["load"] = ("data", "Data / Config"),
        ["json"] = ("data", "Data / Config"),
        ["config"] = ("data", "Data / Config"),
        ["setting"] = ("data", "Data / Config"),
        ["data"] = ("data", "Data / Config"),
        ["audio"] = ("audio", "Audio System"),
        ["sound"] = ("audio", "Audio System"),
        ["bgm"] = ("audio", "Audio System"),
        ["sfx"] = ("audio", "Audio System"),
        ["input"] = ("player", "Player / Input"),
        ["player"] = ("player", "Player / Input"),
        ["camera"] = ("player", "Player / Input")
    };

    private static readonly HashSet<string> WeakTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "base", "common", "core", "type", "types", "kind", "state", "states",
        "manager", "controller", "handler", "service", "system", "model", "models", "view",
        "views", "data", "info", "instance", "item", "items", "object", "objects", "script",
        "scripts", "source", "asset", "assets", "global"
    };

    public static List<GraphSystemCluster> Build(
        IReadOnlyList<GraphNode> nodes,
        IReadOnlyList<GraphEdge> edges,
        IReadOnlyList<GraphMethod> methods,
        IReadOnlyList<GraphMethodEdge> methodEdges)
    {
        var assignments = nodes.ToDictionary(node => node.Id, node => AssignCluster(node), StringComparer.Ordinal);
        var clusters = new Dictionary<string, ClusterDraft>(StringComparer.Ordinal);

        foreach (var node in nodes)
        {
            var assignment = assignments[node.Id];
            if (!clusters.TryGetValue(assignment.Key, out var cluster))
            {
                cluster = new ClusterDraft(assignment.Key, assignment.Name, assignment.Confidence);
                clusters[assignment.Key] = cluster;
            }

            cluster.NodeIds.Add(node.Id);
            foreach (var token in ExtractTokens(node))
            {
                if (!WeakTokens.Contains(token))
                {
                    cluster.Keywords[token] = cluster.Keywords.GetValueOrDefault(token) + 1;
                }
            }
        }

        foreach (var edge in edges)
        {
            if (!assignments.TryGetValue(edge.Source, out var source) || !assignments.TryGetValue(edge.Target, out var target))
            {
                continue;
            }

            if (source.Key == target.Key)
            {
                clusters[source.Key].InternalEdges += Math.Max(1, edge.Weight);
            }
            else
            {
                clusters[source.Key].ExternalEdges += Math.Max(1, edge.Weight);
                clusters[target.Key].ExternalEdges += Math.Max(1, edge.Weight);
            }
        }

        var methodByType = methods
            .GroupBy(method => method.TypeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var methodOutgoing = methodEdges
            .GroupBy(edge => edge.Source, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(edge => edge.Weight), StringComparer.Ordinal);

        foreach (var cluster in clusters.Values)
        {
            cluster.EntryMethodIds.AddRange(cluster.NodeIds
                .SelectMany(id => methodByType.TryGetValue(id, out var typeMethods) ? typeMethods : Enumerable.Empty<GraphMethod>())
                .Where(method => method.IsEntryPoint)
                .OrderByDescending(method => methodOutgoing.GetValueOrDefault(method.Id))
                .ThenBy(method => EntryRank(method.EntryKind))
                .ThenBy(method => method.Line)
                .Take(8)
                .Select(method => method.Id));
        }

        return clusters.Values
            .Where(cluster => cluster.NodeIds.Count > 1 || cluster.InternalEdges > 0 || cluster.EntryMethodIds.Count > 0)
            .OrderByDescending(cluster => cluster.Score)
            .ThenBy(cluster => cluster.Name)
            .Select(cluster => cluster.ToDto())
            .ToList();
    }

    private static (string Key, string Name, int Confidence) AssignCluster(GraphNode node)
    {
        var scores = new Dictionary<string, (string Name, int Score)>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in ExtractTokens(node))
        {
            if (DomainTokens.TryGetValue(token, out var domain))
            {
                AddScore(scores, domain.Key, domain.Name, 5);
            }
        }

        if (scores.Count > 0)
        {
            var best = scores
                .OrderByDescending(pair => pair.Value.Score)
                .ThenBy(pair => pair.Value.Name)
                .First();
            return (best.Key, best.Value.Name, best.Value.Score);
        }

        var fallback = ExtractTokens(node)
            .Where(token => !WeakTokens.Contains(token))
            .GroupBy(token => token, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => group.Key)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(fallback))
        {
            fallback = FolderKey(node.File);
        }

        return ($"topic:{fallback.ToLowerInvariant()}", ToTitle(fallback), 1);
    }

    private static IEnumerable<string> ExtractTokens(GraphNode node)
    {
        foreach (var part in node.Namespace.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var token in SplitWords(part))
            {
                yield return token;
            }
        }

        foreach (var part in FolderParts(node.File))
        {
            foreach (var token in SplitWords(part))
            {
                yield return token;
            }
        }

        foreach (var token in SplitWords(node.Name))
        {
            yield return token;
        }
    }

    private static IEnumerable<string> FolderParts(string file)
    {
        var parts = file.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scriptsIndex = Array.FindIndex(parts, part => part.Equals("Scripts", StringComparison.OrdinalIgnoreCase));
        if (scriptsIndex >= 0)
        {
            foreach (var part in parts.Skip(scriptsIndex + 1).SkipLast(1))
            {
                yield return part;
            }

            yield break;
        }

        if (parts.Length > 1)
        {
            yield return parts[^2];
        }
    }

    private static string FolderKey(string file)
    {
        return FolderParts(file).LastOrDefault(part => !string.IsNullOrWhiteSpace(part)) ?? "Misc";
    }

    private static IEnumerable<string> SplitWords(string value)
    {
        var buffer = new List<char>();
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            var boundary = i > 0
                && char.IsUpper(ch)
                && buffer.Count > 0
                && (char.IsLower(value[i - 1]) || i + 1 < value.Length && char.IsLower(value[i + 1]));

            if (!char.IsLetterOrDigit(ch) || boundary)
            {
                foreach (var token in Flush(buffer))
                {
                    yield return token;
                }

                buffer.Clear();
            }

            if (char.IsLetterOrDigit(ch))
            {
                buffer.Add(char.ToLowerInvariant(ch));
            }
        }

        foreach (var token in Flush(buffer))
        {
            yield return token;
        }
    }

    private static IEnumerable<string> Flush(List<char> buffer)
    {
        if (buffer.Count < 2)
        {
            yield break;
        }

        var token = new string(buffer.ToArray());
        if (!WeakTokens.Contains(token))
        {
            yield return token;
        }
    }

    private static void AddScore(Dictionary<string, (string Name, int Score)> scores, string key, string name, int amount)
    {
        if (scores.TryGetValue(key, out var current))
        {
            scores[key] = (current.Name, current.Score + amount);
        }
        else
        {
            scores[key] = (name, amount);
        }
    }

    private static int EntryRank(string entryKind)
    {
        return entryKind switch
        {
            "unity_lifecycle" => 0,
            "flow_candidate" => 1,
            _ => 2
        };
    }

    private static string ToTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Misc";
        }

        return string.Join(" ", SplitWords(value).DefaultIfEmpty(value.ToLowerInvariant()))
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..])
            .DefaultIfEmpty("Misc")
            .Aggregate((left, right) => $"{left} {right}");
    }

    private sealed class ClusterDraft(string key, string name, int confidence)
    {
        public string Key { get; } = key;
        public string Name { get; } = name;
        public int Confidence { get; } = confidence;
        public HashSet<string> NodeIds { get; } = new(StringComparer.Ordinal);
        public List<string> EntryMethodIds { get; } = new();
        public Dictionary<string, int> Keywords { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int InternalEdges { get; set; }
        public int ExternalEdges { get; set; }
        public int Score => NodeIds.Count * 3 + InternalEdges * 2 + EntryMethodIds.Count + Confidence;

        public GraphSystemCluster ToDto()
        {
            return new GraphSystemCluster
            {
                Id = Key,
                Name = Name,
                Score = Score,
                NodeIds = NodeIds.OrderBy(id => id).ToList(),
                EntryMethodIds = EntryMethodIds.Distinct(StringComparer.Ordinal).ToList(),
                Keywords = Keywords
                    .OrderByDescending(pair => pair.Value)
                    .ThenBy(pair => pair.Key)
                    .Take(12)
                    .Select(pair => pair.Key)
                    .ToList(),
                InternalEdges = InternalEdges,
                ExternalEdges = ExternalEdges
            };
        }
    }
}

internal static class TypeReferenceExtractor
{
    public static IEnumerable<string> Extract(TypeSyntax syntax)
    {
        foreach (var reference in ExtractCore(syntax))
        {
            var cleaned = reference.Trim();
            if (cleaned.Length > 0 && !CodeGraphAnalyzer.IsPrimitive(cleaned))
            {
                yield return cleaned;
            }
        }
    }

    private static IEnumerable<string> ExtractCore(TypeSyntax syntax)
    {
        switch (syntax)
        {
            case IdentifierNameSyntax identifier:
                yield return identifier.Identifier.ValueText;
                break;

            case QualifiedNameSyntax qualified:
                yield return qualified.ToString().Replace("global::", "", StringComparison.Ordinal);
                yield return qualified.Right.Identifier.ValueText;
                break;

            case AliasQualifiedNameSyntax aliasQualified:
                yield return aliasQualified.Name.Identifier.ValueText;
                break;

            case GenericNameSyntax generic:
                yield return generic.Identifier.ValueText;
                foreach (var argument in generic.TypeArgumentList.Arguments)
                {
                    foreach (var reference in ExtractCore(argument))
                    {
                        yield return reference;
                    }
                }
                break;

            case NullableTypeSyntax nullable:
                foreach (var reference in ExtractCore(nullable.ElementType))
                {
                    yield return reference;
                }
                break;

            case ArrayTypeSyntax array:
                foreach (var reference in ExtractCore(array.ElementType))
                {
                    yield return reference;
                }
                break;

            case PointerTypeSyntax pointer:
                foreach (var reference in ExtractCore(pointer.ElementType))
                {
                    yield return reference;
                }
                break;

            case TupleTypeSyntax tuple:
                foreach (var element in tuple.Elements)
                {
                    foreach (var reference in ExtractCore(element.Type))
                    {
                        yield return reference;
                    }
                }
                break;
        }
    }
}

internal sealed class CodeGraph
{
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public required string RootPath { get; init; }
    public required List<string> Files { get; init; }
    public required List<GraphNode> Nodes { get; init; }
    public required List<GraphEdge> Edges { get; init; }
    public required List<GraphMethod> Methods { get; init; }
    public required List<GraphMethodEdge> MethodEdges { get; init; }
    public required List<GraphSystemCluster> SystemClusters { get; init; }
}

internal sealed class GraphNode
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public required string Kind { get; init; }
    public required string File { get; init; }
    public required int Line { get; init; }
    public required List<string> BaseTypes { get; init; }
    public required List<string> Attributes { get; init; }
    public required bool IsUnityType { get; init; }
}

internal sealed class GraphEdge
{
    public required string Source { get; init; }
    public required string Target { get; init; }
    public required string Kind { get; init; }
    public required int Weight { get; set; }
    public required List<EdgeExample> Examples { get; init; }
}

internal sealed class GraphMethod
{
    public required string Id { get; init; }
    public required string TypeId { get; init; }
    public required string Name { get; init; }
    public required string Signature { get; init; }
    public required string Kind { get; init; }
    public required string File { get; init; }
    public required int Line { get; init; }
    public required bool IsEntryPoint { get; init; }
    public required string EntryKind { get; init; }
}

internal sealed class GraphMethodEdge
{
    public required string Source { get; init; }
    public required string Target { get; init; }
    public required string Kind { get; init; }
    public required int Weight { get; set; }
    public required List<EdgeExample> Examples { get; init; }
}

internal sealed class GraphSystemCluster
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int Score { get; init; }
    public required List<string> NodeIds { get; init; }
    public required List<string> EntryMethodIds { get; init; }
    public required List<string> Keywords { get; init; }
    public required int InternalEdges { get; init; }
    public required int ExternalEdges { get; init; }
}

internal sealed class EdgeExample
{
    public required string File { get; init; }
    public required int Line { get; init; }
    public required string Text { get; init; }
}
