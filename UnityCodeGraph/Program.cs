using System.Text.Json;
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

await RunAnalysisAsync(targetPath, outputPath, scanRootNames, jsonOptions);

if (watch)
{
    await WatchAsync(targetPath, outputPath, scanRootNames, jsonOptions);
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
    JsonSerializerOptions jsonOptions)
{
    var analyzer = new CodeGraphAnalyzer();
    var graph = analyzer.Analyze(targetPath, scanRootNames);

    await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(graph, jsonOptions));
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Analyzed {graph.Files.Count} files, {graph.Nodes.Count} types, {graph.Edges.Count} relationships.");
    Console.WriteLine($"Wrote {Path.GetFullPath(outputPath)}");
}

static async Task WatchAsync(
    string targetPath,
    string outputPath,
    IReadOnlySet<string>? scanRootNames,
    JsonSerializerOptions jsonOptions)
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
            await RunAnalysisAsync(targetPath, outputPath, scanRootNames, jsonOptions);
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
    """);
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
            .Select(type => TypeNode.FromSyntax(type, tree))
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

        if (syntax is TypeDeclarationSyntax typeDeclaration && typeDeclaration.BaseList is not null)
        {
            var baseTypes = typeDeclaration.BaseList.Types.Select(b => b.Type).ToList();
            for (var i = 0; i < baseTypes.Count; i++)
            {
                foreach (var reference in TypeReferenceExtractor.Extract(baseTypes[i]))
                {
                    var kind = edges.IsKnownInterface(reference) ? "implements" : i == 0 ? "inherits" : "implements";
                    edges.Add(type.Id, reference, kind, baseTypes[i], type.File);
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

        var fieldVariables = BuildFieldVariableMap(typeWithMembers, edges);

        foreach (var member in typeWithMembers.Members)
        {
            AnalyzeMember(type, member, edges, fieldVariables, methodResolver, methodEdges);
        }
    }

    private static Dictionary<string, string> BuildFieldVariableMap(TypeDeclarationSyntax type, EdgeCollector edges)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in type.Members.OfType<FieldDeclarationSyntax>())
        {
            var reference = ChooseResolvableReference(field.Declaration.Type, edges);
            if (reference is null)
            {
                continue;
            }

            foreach (var variable in field.Declaration.Variables)
            {
                variables[variable.Identifier.ValueText] = reference;
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
                    AddParameterVariable(variableTypes, parameter, edges);
                }
                break;
            case ConstructorDeclarationSyntax constructor:
                foreach (var parameter in constructor.ParameterList.Parameters)
                {
                    AddParameterEdges(owner, edges, parameter);
                    AddParameterVariable(variableTypes, parameter, edges);
                }
                break;
            case OperatorDeclarationSyntax op:
                AddTypeEdges(owner, edges, op.ReturnType, "returns");
                foreach (var parameter in op.ParameterList.Parameters)
                {
                    AddParameterEdges(owner, edges, parameter);
                    AddParameterVariable(variableTypes, parameter, edges);
                }
                break;
            case ConversionOperatorDeclarationSyntax conversion:
                AddTypeEdges(owner, edges, conversion.Type, "returns");
                foreach (var parameter in conversion.ParameterList.Parameters)
                {
                    AddParameterEdges(owner, edges, parameter);
                    AddParameterVariable(variableTypes, parameter, edges);
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
            AddLocalVariables(variableTypes, local, edges);
        }

        foreach (var creation in member.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            AddTypeEdges(owner, edges, creation.Type, "creates");
        }

        foreach (var typeOf in member.DescendantNodes().OfType<TypeOfExpressionSyntax>())
        {
            AddTypeEdges(owner, edges, typeOf.Type, "typeof");
        }

        foreach (var invocation in member.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            AnalyzeInvocation(owner, invocation, edges);
            AnalyzeMemberCall(owner, invocation, edges, variableTypes);
            AnalyzeMethodCall(owner, currentMethod, invocation, methodResolver, methodEdges, variableTypes);
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

    private static void AddParameterVariable(Dictionary<string, string> variableTypes, ParameterSyntax parameter, EdgeCollector edges)
    {
        if (parameter.Type is null)
        {
            return;
        }

        var reference = ChooseResolvableReference(parameter.Type, edges);
        if (reference is not null)
        {
            variableTypes[parameter.Identifier.ValueText] = reference;
        }
    }

    private static void AddLocalVariables(Dictionary<string, string> variableTypes, VariableDeclarationSyntax local, EdgeCollector edges)
    {
        var explicitReference = ChooseResolvableReference(local.Type, edges);

        foreach (var variable in local.Variables)
        {
            var reference = explicitReference ?? InferReferenceFromInitializer(variable.Initializer?.Value, edges);
            if (reference is not null)
            {
                variableTypes[variable.Identifier.ValueText] = reference;
            }
        }
    }

    private static string? InferReferenceFromInitializer(ExpressionSyntax? initializer, EdgeCollector edges)
    {
        return initializer switch
        {
            ObjectCreationExpressionSyntax objectCreation => ChooseResolvableReference(objectCreation.Type, edges),
            CastExpressionSyntax cast => ChooseResolvableReference(cast.Type, edges),
            _ => null
        };
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

        if (memberAccess.Expression is IdentifierNameSyntax receiver
            && variableTypes.TryGetValue(receiver.Identifier.ValueText, out var targetReference))
        {
            edges.Add(owner.Id, targetReference, "calls_member", invocation, owner.File);
        }
    }

    private static void AnalyzeMethodCall(
        TypeNode owner,
        string? currentMethod,
        InvocationExpressionSyntax invocation,
        MethodResolver methodResolver,
        MethodEdgeCollector methodEdges,
        IReadOnlyDictionary<string, string> variableTypes)
    {
        if (currentMethod is null)
        {
            return;
        }

        var target = ResolveMethodTarget(owner, invocation.Expression, methodResolver, variableTypes);
        if (target is null || target == currentMethod)
        {
            return;
        }

        methodEdges.Add(currentMethod, target, "calls", invocation, owner.File);
    }

    private static string? ResolveMethodTarget(
        TypeNode owner,
        ExpressionSyntax expression,
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

                if (memberAccess.Expression is IdentifierNameSyntax receiver
                    && variableTypes.TryGetValue(receiver.Identifier.ValueText, out var targetType))
                {
                    return methodResolver.Resolve(targetType, methodName);
                }

                return null;
            default:
                return null;
        }
    }

    private static string? ChooseResolvableReference(TypeSyntax type, EdgeCollector edges)
    {
        return TypeReferenceExtractor.Extract(type).FirstOrDefault(edges.CanResolve);
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
    public required BaseTypeDeclarationSyntax Syntax { get; init; }

    public static TypeNode FromSyntax(BaseTypeDeclarationSyntax syntax, SyntaxTree tree)
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
}

internal sealed class TypeResolver
{
    private readonly Dictionary<string, List<TypeNode>> _bySimpleName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TypeNode> _byFullName = new(StringComparer.Ordinal);

    public TypeResolver(IEnumerable<TypeNode> nodes)
    {
        foreach (var node in nodes)
        {
            _byFullName[node.Id] = node;
            AddSimple(node.Name.Split('+').Last(), node);
            AddSimple(node.Name, node);
        }
    }

    public TypeNode? Resolve(string reference)
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

    public bool CanResolve(string reference)
    {
        return resolver.Resolve(reference) is not null;
    }

    public void Add(string sourceId, string targetReference, string kind, SyntaxNode syntax, string file)
    {
        var target = resolver.Resolve(targetReference);
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
