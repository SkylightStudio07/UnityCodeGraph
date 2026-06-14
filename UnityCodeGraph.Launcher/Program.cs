using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, eventArgs) => ErrorLog.Write(eventArgs.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception ex)
            {
                ErrorLog.Write(ex);
            }
        };
        Application.Run(new LauncherForm());
    }
}

internal sealed class LauncherForm : Form
{
    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill };
    private Process? _parserProcess;
    private CanvasServer? _canvasServer;

    public LauncherForm()
    {
        Text = "Unity Code Graph";
        Width = 1120;
        Height = 760;
        MinimumSize = new Size(980, 640);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(0, 112, 186);
        Controls.Add(_webView);
        Load += async (_, _) => await InitializeWebViewAsync();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopParser();
        StopCanvasServer();
        base.OnFormClosing(e);
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            await _webView.EnsureCoreWebView2Async();
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            _webView.CoreWebView2.WebMessageReceived += async (_, e) => await HandleMessageAsync(e);

            var indexPath = Path.Combine(AppContext.BaseDirectory, "app", "index.html");
            _webView.CoreWebView2.Navigate(new Uri(indexPath).AbsoluteUri);
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex);
            MessageBox.Show(
                this,
                $"WebView2 launcher failed to start.\n\n{ex.Message}\n\nSee launcher-error.log next to the executable.",
                "Unity Code Graph",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Close();
        }
    }

    private async Task HandleMessageAsync(CoreWebView2WebMessageReceivedEventArgs e)
    {
        using var document = JsonDocument.Parse(e.WebMessageAsJson);
        var root = document.RootElement;
        var type = root.GetProperty("type").GetString();

        try
        {
            switch (type)
            {
                case "ready":
                    PostState();
                    break;
                case "browse":
                    BrowseProjectPath();
                    break;
                case "clone":
                    await CloneRepositoryAsync(GetString(root, "url"));
                    break;
                case "generate":
                    await RunParserAsync(GetSettings(root), watch: false);
                    break;
                case "watch":
                    await RunParserAsync(GetSettings(root), watch: true);
                    break;
                case "stop":
                    StopParser();
                    break;
                case "openCanvas":
                    await OpenCanvasAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}");
        }
    }

    private void BrowseProjectPath()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a Unity project folder or Assets/Scripts folder",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            Send("projectSelected", new
            {
                path = dialog.SelectedPath,
                output = Path.Combine(dialog.SelectedPath, "code-graph.json")
            });
        }
    }

    private async Task CloneRepositoryAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Log("Paste a Git repository URL first.");
            return;
        }

        var target = Path.Combine(Path.GetTempPath(), "UnityCodeGraphRepos", Hash(url));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        if (!Directory.Exists(target))
        {
            Log($"Cloning {url}");
            var exitCode = await RunProcessAsync("git", $"clone --depth 1 \"{url}\" \"{target}\"");
            if (exitCode != 0)
            {
                Log("Git clone failed.");
                return;
            }
        }
        else
        {
            Log($"Using existing clone: {target}");
        }

        Send("projectSelected", new
        {
            path = target,
            output = Path.Combine(target, "code-graph.json")
        });
        Log("Repository ready.");
    }

    private async Task RunParserAsync(LauncherSettings settings, bool watch)
    {
        if (_parserProcess is not null)
        {
            Log("A parser process is already running.");
            return;
        }

        if (!Directory.Exists(settings.ProjectPath) && !File.Exists(settings.ProjectPath))
        {
            Log("Choose a valid project folder first.");
            return;
        }

        var parserPath = FindParserExecutable();
        if (parserPath is null)
        {
            Log("Could not find UnityCodeGraph.exe next to this launcher.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(settings.OutputPath)) ?? Environment.CurrentDirectory);
        var arguments = $"\"{settings.ProjectPath}\" --roots \"{settings.Roots}\" --output \"{settings.OutputPath}\"";
        if (watch)
        {
            arguments += " --watch";
        }

        Log($"> {Path.GetFileName(parserPath)} {arguments}");
        var process = CreateProcess(parserPath, arguments);
        process.EnableRaisingEvents = true;
        process.OutputDataReceived += (_, eventArgs) => Log(eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) => Log(eventArgs.Data);
        process.Exited += (_, _) => BeginInvoke(new Action(() =>
        {
            Log($"Process exited with code {process.ExitCode}.");
            _parserProcess?.Dispose();
            _parserProcess = null;
            Send("runningChanged", new { running = false });
        }));

        _parserProcess = process;
        Send("runningChanged", new { running = true, mode = watch ? "watch" : "generate" });
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!watch)
        {
            await process.WaitForExitAsync();
        }
    }

    private async Task OpenCanvasAsync()
    {
        var workspaceRoot = FindWorkspaceRoot();
        if (workspaceRoot is null)
        {
            Log("Could not find the web canvas files.");
            return;
        }

        var settings = await GetCurrentSettingsAsync();
        var graphPath = File.Exists(settings.OutputPath) ? Path.GetFullPath(settings.OutputPath) : null;
        EnsureCanvasServer(workspaceRoot, graphPath);

        var url = _canvasServer!.BaseUrl + "web/";
        if (graphPath is not null)
        {
            url += "?graph=/graph/current.json";
        }

        Log($"Opening canvas: {url}");
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    private void EnsureCanvasServer(string workspaceRoot, string? graphPath)
    {
        if (_canvasServer is not null && _canvasServer.WorkspaceRoot.Equals(workspaceRoot, StringComparison.OrdinalIgnoreCase))
        {
            _canvasServer.GraphPath = graphPath;
            return;
        }

        StopCanvasServer();

        Log("Starting canvas server.");
        _canvasServer = CanvasServer.Start(workspaceRoot, graphPath);
    }

    private async Task<int> RunProcessAsync(string fileName, string arguments)
    {
        using var process = CreateProcess(fileName, arguments);
        process.OutputDataReceived += (_, eventArgs) => Log(eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) => Log(eventArgs.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private static Process CreateProcess(string fileName, string arguments)
    {
        return new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };
    }

    private void StopParser()
    {
        if (_parserProcess is null)
        {
            return;
        }

        try
        {
            if (!_parserProcess.HasExited)
            {
                _parserProcess.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            Log($"Stop failed: {ex.Message}");
        }
    }

    private void StopCanvasServer()
    {
        _canvasServer?.Dispose();
        _canvasServer = null;
    }

    private void Log(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Send("log", new { message });
    }

    private void PostState()
    {
        Send("state", new
        {
            defaultOutput = Path.Combine(Environment.CurrentDirectory, "code-graph.json"),
            running = _parserProcess is not null
        });
    }

    private void Send(string type, object payload)
    {
        var json = JsonSerializer.Serialize(new { type, payload });
        if (InvokeRequired)
        {
            if (!IsDisposed && !Disposing)
            {
                BeginInvoke(new Action(() => SendJson(json)));
            }
            return;
        }

        SendJson(json);
    }

    private void SendJson(string json)
    {
        if (IsDisposed || Disposing || _webView.CoreWebView2 is null)
        {
            return;
        }

        _webView.CoreWebView2.PostWebMessageAsJson(json);
    }

    private static LauncherSettings GetSettings(JsonElement root)
    {
        return new LauncherSettings(
            GetString(root, "projectPath"),
            GetString(root, "roots"),
            GetString(root, "outputPath"));
    }

    private static string GetString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) ? value.GetString() ?? string.Empty : string.Empty;
    }

    private static string? FindParserExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "UnityCodeGraph.exe"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "UnityCodeGraph", "bin", "Debug", "net9.0", "UnityCodeGraph.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "dist", "UnityCodeGraph-win-x64", "UnityCodeGraph.exe"))
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? FindWorkspaceRoot()
    {
        var starts = new[]
        {
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", ".."))
        };

        foreach (var start in starts)
        {
            var directory = new DirectoryInfo(start);
            for (var i = 0; i < 10 && directory is not null; i++, directory = directory.Parent)
            {
                var webPath = Path.Combine(directory.FullName, "web", "index.html");
                if (File.Exists(webPath))
                {
                    return directory.FullName;
                }
            }
        }

        return null;
    }

    private async Task<LauncherSettings> GetCurrentSettingsAsync()
    {
        if (_webView.CoreWebView2 is null)
        {
            return new LauncherSettings(string.Empty, string.Empty, Path.Combine(Environment.CurrentDirectory, "code-graph.json"));
        }

        var json = await _webView.CoreWebView2.ExecuteScriptAsync("""
            JSON.stringify({
              projectPath: document.querySelector("#projectPath")?.value ?? "",
              roots: document.querySelector("#roots")?.value ?? "",
              outputPath: document.querySelector("#outputPath")?.value ?? ""
            })
            """);
        var encoded = JsonSerializer.Deserialize<string>(json) ?? "{}";
        using var document = JsonDocument.Parse(encoded);
        return GetSettings(document.RootElement);
    }

    private static string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}

internal sealed class CanvasServer : IDisposable
{
    private const int MaxRequestBytes = 2 * 1024 * 1024;
    private const string DefaultOpenAiModel = "gpt-5.4-mini";
    private const string DefaultOpenAiBaseUrl = "https://api.openai.com/v1";
    private const string DefaultOpenRouterModel = "openai/gpt-5.2";
    private const string DefaultOpenRouterBaseUrl = "https://openrouter.ai/api/v1";
    private const string DefaultDeepSeekModel = "deepseek-v4-flash";
    private const string DefaultDeepSeekBaseUrl = "https://api.deepseek.com";
    private const string DefaultOllamaBaseUrl = "http://127.0.0.1:11434";
    private const int AiSummaryTimeoutSeconds = 60;
    private const int AiWorkflowTimeoutSeconds = 120;
    private const string AiSystemPrompt = """
You explain Unity/C# code graphs for developers.

Rules:
- Use only the supplied JSON payload and extracted examples.
- Do not invent files, methods, dependencies, or runtime behavior.
- If the graph evidence is weak or incomplete, say so explicitly.
- Keep the answer practical: describe responsibility, important touchpoints, and likely risks.
- Treat AI output as an interpretation of extracted graph data, not as a compiler-verified fact.
- Respond in the requested language.
""";

    private const string AiNodeInstruction = """
Explain the selected C# type node from this Unity code graph.

Return JSON only, matching the provided schema. Use developer-facing sentences.
For Korean output, explain where this type is called from and what it calls in natural Korean, using the supplied evidence.
Mention concrete caller/callee names when evidence is available. Keep each list concise and useful for a developer navigating the graph.
""";

    private const string AiSystemInstruction = """
Explain the selected system cluster from this Unity code graph.

Return JSON only, matching the provided schema. Describe the cluster's role, major types, likely entry points, internal flow, external touchpoints, and risks.
For Korean output, use practical Korean developer language and refer only to supplied graph evidence. Mention concrete type or method names when evidence is available.
""";

    private const string AiWorkflowInstruction = """
Create a long-form code reading walkthrough for the selected Unity/C# graph target.

Return JSON only, matching the provided schema. This is not a short summary: explain where a developer should start reading, what to inspect next, which methods/types form the likely flow, and what risks or questions to check during review.
Use concrete type, method, file, and line names from the supplied payload whenever evidence exists.
For codeExamples, prefer supplied codeExcerpts when present. Quote only short snippets from supplied examples or codeExcerpts. Do not invent source code.
For Korean output, write natural Korean developer-facing prose.
For workflow output, fill overview, readingPath, importantFlows, codeExamples, risks, and nextQuestions. Prefer 4-6 readingPath steps, 2-4 importantFlows, 1-3 codeExamples, 2-4 risks, and 2-4 nextQuestions when evidence is available.
""";

    private static readonly IReadOnlyDictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".svg"] = "image/svg+xml; charset=utf-8",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp"
    };

    private static readonly HttpClient AiHttp = new()
    {
        Timeout = TimeSpan.FromSeconds(AiWorkflowTimeoutSeconds)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly object AiConfigLock = new();
    private static AiRuntimeConfig AiConfig = LoadInitialAiConfig();

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _loop;

    private CanvasServer(string workspaceRoot, string? graphPath, int port)
    {
        WorkspaceRoot = Path.GetFullPath(workspaceRoot);
        GraphPath = graphPath is null ? null : Path.GetFullPath(graphPath);
        Port = port;
        BaseUrl = $"http://127.0.0.1:{Port}/";
        _listener = new TcpListener(IPAddress.Loopback, Port);
        _listener.Start();
        _loop = Task.Run(ListenAsync);
    }

    public string WorkspaceRoot { get; }
    public string? GraphPath { get; set; }
    public int Port { get; }
    public string BaseUrl { get; }

    public static CanvasServer Start(string workspaceRoot, string? graphPath)
    {
        for (var port = 5173; port < 5200; port++)
        {
            try
            {
                return new CanvasServer(workspaceRoot, graphPath, port);
            }
            catch (SocketException)
            {
                // Port is already taken.
            }
        }

        throw new InvalidOperationException("Could not start a local canvas server on ports 5173-5199.");
    }

    public void Dispose()
    {
        _stopping.Cancel();
        try
        {
            _listener.Stop();
        }
        catch
        {
            // Listener can already be stopped during app shutdown.
        }

        _stopping.Dispose();
    }

    private async Task ListenAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stopping.Token);
            }
            catch
            {
                if (_stopping.IsCancellationRequested)
                {
                    return;
                }

                continue;
            }

            _ = Task.Run(() => HandleAsync(client), _stopping.Token);
        }
    }

    private async Task HandleAsync(TcpClient client)
    {
        using var _ = client;
        await using var stream = client.GetStream();

        try
        {
            var request = await ReadRequestAsync(stream);
            if (request is null)
            {
                return;
            }

            if (request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase)
                && request.Path.Equals("/graph/current.json", StringComparison.OrdinalIgnoreCase))
            {
                await ServeGraphAsync(stream);
                return;
            }

            if (request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase)
                && request.Path.Equals("/ai/status", StringComparison.OrdinalIgnoreCase))
            {
                await ServeAiStatusAsync(stream);
                return;
            }

            if (request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase)
                && request.Path.Equals("/ai/config", StringComparison.OrdinalIgnoreCase))
            {
                await ServeAiConfigAsync(stream);
                return;
            }

            if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
                && request.Path.Equals("/ai/config", StringComparison.OrdinalIgnoreCase))
            {
                await ServeAiConfigUpdateAsync(stream, request.Body);
                return;
            }

            if (request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase)
                && request.Path.Equals("/ai/models", StringComparison.OrdinalIgnoreCase))
            {
                await ServeAiModelsAsync(stream);
                return;
            }

            if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
                && request.Path.Equals("/ai/explain-node", StringComparison.OrdinalIgnoreCase))
            {
                await ServeAiExplainAsync(stream, request.Body, AiNodeInstruction, "unity_code_graph_node_summary");
                return;
            }

            if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
                && request.Path.Equals("/ai/explain-system", StringComparison.OrdinalIgnoreCase))
            {
                await ServeAiExplainAsync(stream, request.Body, AiSystemInstruction, "unity_code_graph_system_summary");
                return;
            }

            if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
                && request.Path.Equals("/ai/explain-workflow", StringComparison.OrdinalIgnoreCase))
            {
                await ServeAiExplainAsync(stream, request.Body, AiWorkflowInstruction, "unity_code_graph_workflow", true);
                return;
            }

            if (!request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                await WriteTextAsync(stream, 405, "Method Not Allowed");
                return;
            }

            await ServeStaticAsync(stream, request.Path);
        }
        catch
        {
            if (stream.CanWrite)
            {
                await WriteTextAsync(stream, 500, "Server error");
            }
        }
    }

    private static async Task<HttpRequest?> ReadRequestAsync(Stream stream)
    {
        var buffer = new byte[8192];
        using var requestBytes = new MemoryStream();
        var headerEnd = -1;

        while (headerEnd < 0)
        {
            var read = await stream.ReadAsync(buffer);
            if (read <= 0)
            {
                return null;
            }

            requestBytes.Write(buffer, 0, read);
            if (requestBytes.Length > MaxRequestBytes)
            {
                throw new InvalidOperationException("Request is too large.");
            }

            var bytes = requestBytes.ToArray();
            headerEnd = FindHeaderEnd(bytes);
        }

        var allBytes = requestBytes.ToArray();
        var headerText = Encoding.ASCII.GetString(allBytes, 0, headerEnd);
        var headerLines = headerText.Split("\r\n", StringSplitOptions.None);
        var parts = headerLines[0].Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in headerLines.Skip(1))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0) continue;
            headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }

        var contentLength = headers.TryGetValue("Content-Length", out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : 0;
        if (contentLength > MaxRequestBytes)
        {
            throw new InvalidOperationException("Request body is too large.");
        }

        var bodyBytes = new byte[contentLength];
        var alreadyRead = Math.Min(contentLength, allBytes.Length - headerEnd);
        if (alreadyRead > 0)
        {
            Array.Copy(allBytes, headerEnd, bodyBytes, 0, alreadyRead);
        }

        while (alreadyRead < contentLength)
        {
            var read = await stream.ReadAsync(bodyBytes.AsMemory(alreadyRead, contentLength - alreadyRead));
            if (read <= 0)
            {
                break;
            }

            alreadyRead += read;
        }

        var path = Uri.UnescapeDataString(new Uri(parts[1], UriKind.RelativeOrAbsolute).IsAbsoluteUri
            ? new Uri(parts[1]).AbsolutePath
            : parts[1].Split('?', 2)[0]);
        var body = Encoding.UTF8.GetString(bodyBytes, 0, alreadyRead);
        return new HttpRequest(parts[0], path, body);
    }

    private static int FindHeaderEnd(byte[] bytes)
    {
        for (var i = 3; i < bytes.Length; i++)
        {
            if (bytes[i - 3] == '\r' && bytes[i - 2] == '\n' && bytes[i - 1] == '\r' && bytes[i] == '\n')
            {
                return i + 1;
            }
        }

        return -1;
    }

    private sealed record HttpRequest(string Method, string Path, string Body);

    private sealed record AiRuntimeConfig(string Provider, string BaseUrl, string ApiKey, string Model)
    {
        public static AiRuntimeConfig FromEnvironment()
        {
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")?.Trim() ?? "";
            var model = Environment.GetEnvironmentVariable("OPENAI_MODEL")?.Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                var openRouterApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")?.Trim() ?? "";
                var openRouterModel = Environment.GetEnvironmentVariable("OPENROUTER_MODEL")?.Trim();
                if (!string.IsNullOrWhiteSpace(openRouterApiKey))
                {
                    return new AiRuntimeConfig(
                        "openrouter",
                        DefaultOpenRouterBaseUrl,
                        openRouterApiKey,
                        string.IsNullOrWhiteSpace(openRouterModel) ? DefaultOpenRouterModel : openRouterModel);
                }

                var deepSeekApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")?.Trim() ?? "";
                var deepSeekModel = Environment.GetEnvironmentVariable("DEEPSEEK_MODEL")?.Trim();
                if (!string.IsNullOrWhiteSpace(deepSeekApiKey))
                {
                    return new AiRuntimeConfig(
                        "deepseek",
                        DefaultDeepSeekBaseUrl,
                        deepSeekApiKey,
                        string.IsNullOrWhiteSpace(deepSeekModel) ? DefaultDeepSeekModel : deepSeekModel);
                }
            }

            return new AiRuntimeConfig(
                string.IsNullOrWhiteSpace(apiKey) ? "disabled" : "openai",
                DefaultOpenAiBaseUrl,
                apiKey,
                string.IsNullOrWhiteSpace(model) ? DefaultOpenAiModel : model);
        }
    }

    private sealed record AiSettingsFile(string Provider, string BaseUrl, string Model, string ProtectedApiKey);

    private static string AiSettingsPath
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UnityCodeGraph",
            "ai-settings.json");

    private static AiRuntimeConfig LoadInitialAiConfig()
    {
        var saved = TryLoadSavedAiConfig();
        return saved is not null
            ? NormalizeAiConfig(saved)
            : AiRuntimeConfig.FromEnvironment();
    }

    private static AiRuntimeConfig? TryLoadSavedAiConfig()
    {
        try
        {
            if (!File.Exists(AiSettingsPath))
            {
                return null;
            }

            var settings = JsonSerializer.Deserialize<AiSettingsFile>(File.ReadAllText(AiSettingsPath), JsonOptions);
            if (settings is null)
            {
                return null;
            }

            var apiKey = UnprotectSecret(settings.ProtectedApiKey);
            return new AiRuntimeConfig(settings.Provider, settings.BaseUrl, apiKey, settings.Model);
        }
        catch (Exception exception)
        {
            ErrorLog.Write(exception);
            return null;
        }
    }

    private static bool TrySaveAiConfig(AiRuntimeConfig config)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AiSettingsPath)!);
            var settings = new AiSettingsFile(
                config.Provider,
                config.BaseUrl,
                config.Model,
                ProtectSecret(config.ApiKey));
            File.WriteAllText(AiSettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
            return true;
        }
        catch (Exception exception)
        {
            ErrorLog.Write(exception);
            return false;
        }
    }

    private static string ProtectSecret(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    private static string UnprotectSecret(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var bytes = ProtectedData.Unprotect(Convert.FromBase64String(value), null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }

    private static AiRuntimeConfig CurrentAiConfig()
    {
        lock (AiConfigLock)
        {
            return AiConfig;
        }
    }

    private static AiRuntimeConfig NormalizeAiConfig(AiRuntimeConfig config)
    {
        var provider = NormalizeProvider(config.Provider);
        var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? DefaultBaseUrl(provider) : config.BaseUrl.Trim();
        var model = string.IsNullOrWhiteSpace(config.Model) ? DefaultModel(provider) : config.Model.Trim();
        return new AiRuntimeConfig(provider, baseUrl.TrimEnd('/'), config.ApiKey.Trim(), model);
    }

    private static string NormalizeProvider(string? provider)
        => (provider ?? "").Trim().ToLowerInvariant() switch
        {
            "openai" => "openai",
            "openrouter" => "openrouter",
            "open-router" => "openrouter",
            "deepseek" => "deepseek",
            "compatible" => "compatible",
            "openai-compatible" => "compatible",
            "ollama" => "ollama",
            "vertex" => "vertex",
            _ => "disabled"
        };

    private static string DefaultBaseUrl(string provider)
        => provider switch
        {
            "openai" => DefaultOpenAiBaseUrl,
            "openrouter" => DefaultOpenRouterBaseUrl,
            "deepseek" => DefaultDeepSeekBaseUrl,
            "compatible" => DefaultOpenAiBaseUrl,
            "ollama" => DefaultOllamaBaseUrl,
            _ => ""
        };

    private static string DefaultModel(string provider)
        => provider switch
        {
            "openai" => DefaultOpenAiModel,
            "openrouter" => DefaultOpenRouterModel,
            "deepseek" => DefaultDeepSeekModel,
            "compatible" => DefaultOpenAiModel,
            "ollama" => "qwen3-coder",
            "vertex" => "gemini-3.5-flash",
            _ => ""
        };

    private static bool IsAiConfigured(AiRuntimeConfig config, out string reason)
    {
        reason = "";
        if (config.Provider == "disabled")
        {
            reason = "AI provider is disabled";
            return false;
        }

        if (config.Provider == "vertex")
        {
            reason = "Vertex provider is planned but not implemented yet";
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.BaseUrl))
        {
            reason = "Provider base URL is not set";
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.Model))
        {
            reason = "Model is not selected";
            return false;
        }

        if ((config.Provider == "openai" || config.Provider == "openrouter" || config.Provider == "deepseek") && string.IsNullOrWhiteSpace(config.ApiKey))
        {
            reason = config.Provider switch
            {
                "openrouter" => "OpenRouter API key is not set",
                "deepseek" => "DeepSeek API key is not set",
                _ => "OPENAI_API_KEY is not set"
            };
            return false;
        }

        return true;
    }

    private async Task ServeGraphAsync(Stream stream)
    {
        var graphPath = GraphPath;
        if (string.IsNullOrWhiteSpace(graphPath) || !File.Exists(graphPath))
        {
            await WriteTextAsync(stream, 404, "Graph JSON not found");
            return;
        }

        await WriteFileAsync(stream, graphPath, "application/json; charset=utf-8");
    }

    private static async Task ServeAiStatusAsync(Stream stream)
    {
        var config = CurrentAiConfig();
        var enabled = IsAiConfigured(config, out var reason);
        var payload = new
        {
            enabled,
            provider = config.Provider,
            model = config.Model,
            baseUrl = config.BaseUrl,
            apiKeyConfigured = !string.IsNullOrWhiteSpace(config.ApiKey),
            apiKeyStored = File.Exists(AiSettingsPath),
            modelSuggestions = ModelSuggestions(config.Provider),
            reason
        };

        await WriteJsonAsync(stream, payload);
    }

    private static async Task ServeAiConfigAsync(Stream stream)
    {
        var config = CurrentAiConfig();
        await WriteJsonAsync(stream, AiConfigPayload(config));
    }

    private static async Task ServeAiConfigUpdateAsync(Stream stream, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            await WriteJsonAsync(stream, 400, new { error = "Request body is empty" });
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var current = CurrentAiConfig();
            var provider = JsonString(root, "provider", current.Provider);
            var normalizedProvider = NormalizeProvider(provider);
            var baseUrl = JsonString(root, "baseUrl", string.IsNullOrWhiteSpace(current.BaseUrl) ? DefaultBaseUrl(normalizedProvider) : current.BaseUrl);
            var model = JsonString(root, "model", string.IsNullOrWhiteSpace(current.Model) ? DefaultModel(normalizedProvider) : current.Model);
            var hasApiKeyInput = root.TryGetProperty("apiKey", out var apiKeyElement) && apiKeyElement.ValueKind == JsonValueKind.String;
            var saveApiKey = root.TryGetProperty("saveApiKey", out var saveApiKeyElement)
                && saveApiKeyElement.ValueKind == JsonValueKind.True;
            var apiKey = hasApiKeyInput
                ? apiKeyElement.GetString() ?? ""
                : normalizedProvider.Equals(current.Provider, StringComparison.OrdinalIgnoreCase)
                    ? current.ApiKey
                    : "";

            if (normalizedProvider == "openai" && string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")?.Trim() ?? "";
            }
            else if (normalizedProvider == "openrouter" && string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")?.Trim() ?? "";
            }
            else if (normalizedProvider == "deepseek" && string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")?.Trim() ?? "";
            }

            var next = NormalizeAiConfig(new AiRuntimeConfig(normalizedProvider, baseUrl, apiKey, model));
            lock (AiConfigLock)
            {
                AiConfig = next;
            }

            var saved = saveApiKey && TrySaveAiConfig(next);
            await WriteJsonAsync(stream, AiConfigPayload(next, saved));
        }
        catch (JsonException)
        {
            await WriteJsonAsync(stream, 400, new { error = "Request body must be valid JSON" });
        }
    }

    private static object AiConfigPayload(AiRuntimeConfig config, bool? saved = null)
    {
        var enabled = IsAiConfigured(config, out var reason);
        return new
        {
            enabled,
            provider = config.Provider,
            baseUrl = config.BaseUrl,
            model = config.Model,
            apiKeyConfigured = !string.IsNullOrWhiteSpace(config.ApiKey),
            apiKeyStored = File.Exists(AiSettingsPath),
            saved,
            reason,
            modelSuggestions = ModelSuggestions(config.Provider)
        };
    }

    private static string JsonString(JsonElement root, string name, string fallback)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;

    private static async Task ServeAiModelsAsync(Stream stream)
    {
        var config = CurrentAiConfig();
        var models = ModelSuggestions(config.Provider).ToList();

        try
        {
            if (config.Provider == "ollama" && !string.IsNullOrWhiteSpace(config.BaseUrl))
            {
                models = await FetchOllamaModelsAsync(config);
            }
            else if ((config.Provider == "openai" || config.Provider == "openrouter" || config.Provider == "compatible")
                && !string.IsNullOrWhiteSpace(config.BaseUrl))
            {
                models = await FetchOpenAiCompatibleModelsAsync(config);
            }
        }
        catch
        {
            // Keep static suggestions when provider model discovery fails.
        }

        await WriteJsonAsync(stream, new
        {
            provider = config.Provider,
            models = models.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        });
    }

    private static IEnumerable<string> ModelSuggestions(string provider)
        => provider switch
        {
            "openai" => new[] { DefaultOpenAiModel, "gpt-5.5", "gpt-5.4", "gpt-5.4-nano", "gpt-5" },
            "openrouter" => new[]
            {
                DefaultOpenRouterModel,
                "anthropic/claude-sonnet-4.6",
                "google/gemini-3.5-flash",
                "deepseek/deepseek-v4-flash",
                "qwen/qwen3.7-max",
                "moonshotai/kimi-k2.7-code"
            },
            "deepseek" => new[] { DefaultDeepSeekModel, "deepseek-v4-pro", "deepseek-chat", "deepseek-reasoner" },
            "compatible" => new[]
            {
                "deepseek-v4-flash",
                "deepseek-v4-pro",
                "claude-sonnet-4-6",
                "claude-opus-4-8",
                "gemini-3.5-flash",
                "gemini-3.1-pro",
                "moonshotai/kimi-k2.7-code",
                "qwen/qwen3.7-max"
            },
            "ollama" => new[] { "qwen3-coder", "gpt-oss", "gemma4", "deepseek-r1", "qwen3.6", "llama4", "glm-4.7-flash" },
            "vertex" => new[] { "gemini-3.5-flash", "gemini-3.1-flash-lite", "gemini-2.5-pro", "gemini-2.5-flash" },
            _ => Array.Empty<string>()
        };

    private static async Task<List<string>> FetchOpenAiCompatibleModelsAsync(AiRuntimeConfig config)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{config.BaseUrl.TrimEnd('/')}/models");
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        }
        AddOpenRouterHeaders(request, config);

        using var response = await AiHttp.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(text);
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return ModelSuggestions(config.Provider).ToList();
        }

        return data.EnumerateArray()
            .Select(item => item.TryGetProperty("id", out var id) ? id.GetString() : null)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<List<string>> FetchOllamaModelsAsync(AiRuntimeConfig config)
    {
        using var response = await AiHttp.GetAsync($"{config.BaseUrl.TrimEnd('/')}/api/tags");
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(text);
        if (!document.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
        {
            return ModelSuggestions("ollama").ToList();
        }

        return models.EnumerateArray()
            .Select(item => item.TryGetProperty("name", out var name) ? name.GetString() : null)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task ServeAiExplainAsync(Stream stream, string body, string instruction, string schemaName, bool workflow = false)
    {
        var config = CurrentAiConfig();
        if (!IsAiConfigured(config, out var reason))
        {
            await WriteJsonAsync(stream, 503, new { error = reason });
            return;
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            await WriteJsonAsync(stream, 400, new { error = "Request body is empty" });
            return;
        }

        try
        {
            using var _ = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            await WriteJsonAsync(stream, 400, new { error = "Request body must be valid JSON" });
            return;
        }

        try
        {
            var enrichedBody = EnrichAiPayloadWithCodeExcerpts(body, workflow);
            var outputText = config.Provider switch
            {
                "openai" => await RequestOpenAiResponseAsync(config, enrichedBody, instruction, schemaName, workflow),
                "openrouter" => await RequestOpenAiCompatibleChatAsync(config, enrichedBody, instruction, schemaName, workflow),
                "deepseek" => await RequestOpenAiCompatibleChatAsync(config, enrichedBody, instruction, schemaName, workflow),
                "compatible" => await RequestOpenAiCompatibleChatAsync(config, enrichedBody, instruction, schemaName, workflow),
                "ollama" => await RequestOllamaChatAsync(config, enrichedBody, instruction, workflow),
                _ => ""
            };
            if (string.IsNullOrWhiteSpace(outputText))
            {
                await WriteJsonAsync(stream, 502, new { error = "AI response did not contain output text" });
                return;
            }

            JsonElement result;
            try
            {
                var jsonText = ExtractJsonObjectText(outputText);
                result = JsonSerializer.Deserialize<JsonElement>(jsonText);
            }
            catch (JsonException exception)
            {
                throw new AiRequestException($"AI response was not valid JSON: {exception.Message}. Raw response: {ClipForError(outputText)}");
            }

            await WriteJsonAsync(stream, new
            {
                provider = config.Provider,
                model = config.Model,
                createdAt = DateTimeOffset.UtcNow,
                result
            });
        }
        catch (JsonException)
        {
            await WriteJsonAsync(stream, 502, new { error = "AI response was not valid summary JSON" });
        }
        catch (TaskCanceledException)
        {
            await WriteJsonAsync(stream, 504, new { error = "AI request timed out" });
        }
        catch (AiRequestException exception)
        {
            await WriteJsonAsync(stream, 502, new { error = exception.Message });
        }
        catch (HttpRequestException exception)
        {
            await WriteJsonAsync(stream, 502, new { error = $"AI request failed: {exception.Message}" });
        }
    }

    private static async Task<string> RequestOpenAiResponseAsync(AiRuntimeConfig config, string payload, string instruction, string schemaName, bool workflow)
    {
        var requestPayload = new
        {
            model = config.Model,
            instructions = AiSystemPrompt,
            input = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "input_text",
                            text = $"{instruction}\n\nPayload:\n{payload}"
                        }
                    }
                }
            },
            text = new
            {
                format = SummaryResponseFormat(schemaName, workflow)
            },
            max_output_tokens = workflow ? 4096 : 900,
            store = false
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{config.BaseUrl.TrimEnd('/')}/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestPayload, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await AiHttp.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();
        EnsureAiSuccess(response, responseText);
        return ExtractResponseOutputText(responseText);
    }

    private static async Task<string> RequestOpenAiCompatibleChatAsync(AiRuntimeConfig config, string payload, string instruction, string schemaName, bool workflow)
    {
        var jsonObjectMode = UsesJsonObjectMode(config);
        var schemaPrompt = jsonObjectMode ? $"\n\nExpected JSON shape:\n{JsonObjectSchemaPrompt(workflow)}" : "";
        var requestPayload = new
        {
            model = config.Model,
            messages = new object[]
            {
                new { role = "system", content = AiSystemPrompt },
                new { role = "user", content = $"{instruction}{schemaPrompt}\n\nPayload:\n{payload}" + (jsonObjectMode ? "\n\nReturn exactly one valid JSON object. Do not use markdown." : "") }
            },
            response_format = jsonObjectMode ? JsonObjectResponseFormat() : SummaryChatResponseFormat(schemaName, workflow),
            temperature = 0.2,
            max_tokens = workflow ? 4096 : 900
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{config.BaseUrl.TrimEnd('/')}/chat/completions");
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        }
        AddOpenRouterHeaders(request, config);

        request.Content = new StringContent(JsonSerializer.Serialize(requestPayload, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await AiHttp.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();
        EnsureAiSuccess(response, responseText);
        return ExtractChatOutputText(responseText);
    }

    private static bool UsesJsonObjectMode(AiRuntimeConfig config)
        => config.Provider.Equals("deepseek", StringComparison.OrdinalIgnoreCase)
            || config.BaseUrl.Contains("deepseek.com", StringComparison.OrdinalIgnoreCase);

    private static void EnsureAiSuccess(HttpResponseMessage response, string responseText)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = ProviderErrorDetail(responseText);
        throw new AiRequestException($"AI request failed ({(int)response.StatusCode} {response.ReasonPhrase}): {detail}");
    }

    private static string ProviderErrorDetail(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return "provider returned an empty error body";
        }

        try
        {
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                {
                    return error.GetString() ?? "";
                }

                if (error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                {
                    return message.GetString() ?? "";
                }
            }

            if (root.TryGetProperty("message", out var rootMessage) && rootMessage.ValueKind == JsonValueKind.String)
            {
                return rootMessage.GetString() ?? "";
            }
        }
        catch (JsonException)
        {
            // Fall through to clipped raw text.
        }

        return responseText.Length > 500 ? responseText[..500] : responseText;
    }

    private string EnrichAiPayloadWithCodeExcerpts(string body, bool workflow)
    {
        if (!workflow)
        {
            return body;
        }

        try
        {
            var root = JsonNode.Parse(body) as JsonObject;
            if (root is null)
            {
                return body;
            }

            var projectRoot = FindGraphRootPath(root);
            var locations = new List<SourceLocation>();
            CollectSourceLocations(root, locations);

            var excerpts = new JsonArray();
            foreach (var location in locations
                .Where(item => item.Line > 0 && !string.IsNullOrWhiteSpace(item.File))
                .OrderBy(item => item.Priority)
                .DistinctBy(item => $"{item.File}|{item.Line}")
                .Take(10))
            {
                var excerpt = TryReadCodeExcerpt(location, projectRoot);
                if (excerpt is not null)
                {
                    excerpts.Add(excerpt);
                }
            }

            if (excerpts.Count == 0)
            {
                return body;
            }

            root["codeExcerpts"] = excerpts;
            return root.ToJsonString(JsonOptions);
        }
        catch
        {
            return body;
        }
    }

    private string? FindGraphRootPath(JsonObject payload)
    {
        var fromPayload = JsonStringPath(payload["graph"], "rootPath")
            ?? JsonStringPath(payload, "rootPath");
        if (!string.IsNullOrWhiteSpace(fromPayload) && Directory.Exists(fromPayload))
        {
            return Path.GetFullPath(fromPayload);
        }

        var graphPath = GraphPath;
        if (string.IsNullOrWhiteSpace(graphPath) || !File.Exists(graphPath))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(graphPath));
            if (document.RootElement.TryGetProperty("RootPath", out var rootPath)
                && rootPath.ValueKind == JsonValueKind.String
                && Directory.Exists(rootPath.GetString()))
            {
                return Path.GetFullPath(rootPath.GetString()!);
            }
        }
        catch
        {
            // Root path lookup is best effort.
        }

        return null;
    }

    private static string? JsonStringPath(JsonNode? node, string propertyName)
        => node is JsonObject obj
            && obj.TryGetPropertyValue(propertyName, out var value)
            && value is JsonValue jsonValue
            && jsonValue.TryGetValue<string>(out var text)
            ? text
            : null;

    private static void CollectSourceLocations(JsonNode? node, List<SourceLocation> locations)
    {
        switch (node)
        {
            case JsonObject obj:
                if (TrySourceLocation(obj, out var location))
                {
                    locations.Add(location);
                }

                foreach (var child in obj.Select(pair => pair.Value))
                {
                    CollectSourceLocations(child, locations);
                }
                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    CollectSourceLocations(child, locations);
                }
                break;
        }
    }

    private static bool TrySourceLocation(JsonObject obj, out SourceLocation location)
    {
        location = default;
        var file = JsonStringPath(obj, "file");
        if (string.IsNullOrWhiteSpace(file)
            || !obj.TryGetPropertyValue("line", out var lineNode)
            || lineNode is not JsonValue lineValue
            || !lineValue.TryGetValue<int>(out var line)
            || line <= 0)
        {
            return false;
        }

        location = new SourceLocation(file, line, SourceLocationPriority(obj));
        return true;
    }

    private static int SourceLocationPriority(JsonObject obj)
    {
        if (obj.ContainsKey("signature") || obj.ContainsKey("entryKind"))
        {
            return 0;
        }

        if (obj.ContainsKey("text"))
        {
            return 1;
        }

        if (obj.ContainsKey("code"))
        {
            return 2;
        }

        if (obj.ContainsKey("kind") || obj.ContainsKey("namespace"))
        {
            return 4;
        }

        return 3;
    }

    private JsonObject? TryReadCodeExcerpt(SourceLocation location, string? projectRoot)
    {
        var path = ResolveSourcePath(location.File, projectRoot);
        if (path is null || !File.Exists(path) || !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!IsAllowedSourcePath(path, projectRoot))
        {
            return null;
        }

        var lines = File.ReadAllLines(path);
        if (lines.Length == 0)
        {
            return null;
        }

        var marker = Math.Clamp(location.Line, 1, lines.Length);
        var start = Math.Max(1, marker - 5);
        var end = Math.Min(lines.Length, marker + 7);
        var builder = new StringBuilder();
        for (var lineNumber = start; lineNumber <= end; lineNumber++)
        {
            var prefix = lineNumber == marker ? ">" : " ";
            builder.Append(prefix);
            builder.Append(lineNumber.ToString().PadLeft(4));
            builder.Append(" | ");
            builder.AppendLine(lines[lineNumber - 1]);
        }

        return new JsonObject
        {
            ["file"] = path,
            ["line"] = marker,
            ["startLine"] = start,
            ["endLine"] = end,
            ["code"] = builder.ToString().TrimEnd()
        };
    }

    private string? ResolveSourcePath(string file, string? projectRoot)
    {
        var path = file.Trim();
        if (Path.IsPathFullyQualified(path))
        {
            return Path.GetFullPath(path);
        }

        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            var candidate = Path.GetFullPath(Path.Combine(projectRoot, path));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var workspaceCandidate = Path.GetFullPath(Path.Combine(WorkspaceRoot, path));
        return File.Exists(workspaceCandidate) ? workspaceCandidate : null;
    }

    private bool IsAllowedSourcePath(string path, string? projectRoot)
    {
        var fullPath = Path.GetFullPath(path);
        if (!string.IsNullOrWhiteSpace(projectRoot) && IsUnderRoot(fullPath, projectRoot))
        {
            return true;
        }

        return IsUnderRoot(fullPath, WorkspaceRoot);
    }

    private static string ExtractJsonObjectText(string outputText)
    {
        var text = StripCodeFence(outputText).Trim();
        if (text.StartsWith('{') && text.EndsWith('}'))
        {
            return text;
        }

        var start = text.IndexOf('{');
        if (start < 0)
        {
            throw new JsonException("No JSON object was found in the AI response.");
        }

        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return text[start..(i + 1)];
                }
            }
        }

        throw new JsonException("The AI response contained an incomplete JSON object. The provider likely stopped before finishing the JSON; try regenerating or using a larger output model.");
    }

    private static string StripCodeFence(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstLineEnd = trimmed.IndexOf('\n');
        if (firstLineEnd < 0)
        {
            return trimmed;
        }

        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (lastFence <= firstLineEnd)
        {
            return trimmed[(firstLineEnd + 1)..];
        }

        return trimmed[(firstLineEnd + 1)..lastFence].Trim();
    }

    private static string ClipForError(string text)
    {
        var clipped = text.Trim().Replace("\r", " ").Replace("\n", " ");
        return clipped.Length > 500 ? clipped[..500] : clipped;
    }

    private static void AddOpenRouterHeaders(HttpRequestMessage request, AiRuntimeConfig config)
    {
        if (!config.Provider.Equals("openrouter", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://github.com/");
        request.Headers.TryAddWithoutValidation("X-OpenRouter-Title", "Unity Code Graph");
    }

    private static async Task<string> RequestOllamaChatAsync(AiRuntimeConfig config, string payload, string instruction, bool workflow)
    {
        var requestPayload = new
        {
            model = config.Model,
            stream = false,
            messages = new object[]
            {
                new { role = "system", content = AiSystemPrompt },
                new { role = "user", content = $"{instruction}\n\nPayload:\n{payload}" }
            },
            format = SummarySchema(workflow)
        };

        using var response = await AiHttp.PostAsync(
            $"{config.BaseUrl.TrimEnd('/')}/api/chat",
            new StringContent(JsonSerializer.Serialize(requestPayload, JsonOptions), Encoding.UTF8, "application/json"));
        var responseText = await response.Content.ReadAsStringAsync();
        EnsureAiSuccess(response, responseText);
        return ExtractOllamaOutputText(responseText);
    }

    private static object SummaryResponseFormat(string schemaName, bool workflow)
        => new
        {
            type = "json_schema",
            name = schemaName,
            strict = true,
            schema = SummarySchema(workflow)
        };

    private static object SummaryChatResponseFormat(string schemaName, bool workflow)
        => new
        {
            type = "json_schema",
            json_schema = new
            {
                name = schemaName,
                strict = true,
                schema = SummarySchema(workflow)
            }
        };

    private static object JsonObjectResponseFormat()
        => new
        {
            type = "json_object"
        };

    private static string JsonObjectSchemaPrompt(bool workflow)
        => workflow
            ? """
{
  "title": "string",
  "overview": "string",
  "readingPath": [
    {
      "stepTitle": "string",
      "why": "string",
      "inspect": "string",
      "evidenceRefs": ["string"]
    }
  ],
  "importantFlows": ["string"],
  "codeExamples": [
    {
      "title": "string",
      "file": "string",
      "line": 0,
      "code": "string",
      "why": "string"
    }
  ],
  "risks": ["string"],
  "nextQuestions": ["string"],
  "confidence": "low|medium|high",
  "disclaimer": "string"
}
"""
            : """
{
  "summary": "string",
  "responsibilities": ["string"],
  "touchpoints": ["string"],
  "risks": ["string"],
  "confidence": "low|medium|high",
  "disclaimer": "string"
}
""";

    private static object SummarySchema(bool workflow)
        => workflow ? WorkflowSchema() : NodeSummarySchema();

    private static object NodeSummarySchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            properties = new
            {
                summary = new { type = "string" },
                responsibilities = StringArraySchema(),
                touchpoints = StringArraySchema(),
                risks = StringArraySchema(),
                confidence = new { type = "string", @enum = new[] { "low", "medium", "high" } },
                disclaimer = new { type = "string" }
            },
            required = new[] { "summary", "responsibilities", "touchpoints", "risks", "confidence", "disclaimer" }
        };

    private static object WorkflowSchema()
        => new
        {
            type = "object",
            additionalProperties = false,
            properties = new
            {
                title = new { type = "string" },
                overview = new { type = "string" },
                readingPath = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        additionalProperties = false,
                        properties = new
                        {
                            stepTitle = new { type = "string" },
                            why = new { type = "string" },
                            inspect = new { type = "string" },
                            evidenceRefs = StringArraySchema()
                        },
                        required = new[] { "stepTitle", "why", "inspect", "evidenceRefs" }
                    }
                },
                importantFlows = StringArraySchema(),
                codeExamples = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        additionalProperties = false,
                        properties = new
                        {
                            title = new { type = "string" },
                            file = new { type = "string" },
                            line = new { type = "integer" },
                            code = new { type = "string" },
                            why = new { type = "string" }
                        },
                        required = new[] { "title", "file", "line", "code", "why" }
                    }
                },
                risks = StringArraySchema(),
                nextQuestions = StringArraySchema(),
                confidence = new { type = "string", @enum = new[] { "low", "medium", "high" } },
                disclaimer = new { type = "string" }
            },
            required = new[] { "title", "overview", "readingPath", "importantFlows", "codeExamples", "risks", "nextQuestions", "confidence", "disclaimer" }
        };

    private static object StringArraySchema()
        => new
        {
            type = "array",
            items = new { type = "string" }
        };

    private static string ExtractResponseOutputText(string responseText)
    {
        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;
        if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
        {
            return outputText.GetString() ?? "";
        }

        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return "";
        }

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var type)
                    && type.GetString() == "output_text"
                    && part.TryGetProperty("text", out var text)
                    && text.ValueKind == JsonValueKind.String)
                {
                    return text.GetString() ?? "";
                }
            }
        }

        return "";
    }

    private static string ExtractChatOutputText(string responseText)
    {
        using var document = JsonDocument.Parse(responseText);
        if (!document.RootElement.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
        {
            return "";
        }

        foreach (var choice in choices.EnumerateArray())
        {
            if (choice.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.String)
            {
                return content.GetString() ?? "";
            }
        }

        return "";
    }

    private static string ExtractOllamaOutputText(string responseText)
    {
        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;
        if (root.TryGetProperty("message", out var message)
            && message.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? "";
        }

        if (root.TryGetProperty("response", out var response) && response.ValueKind == JsonValueKind.String)
        {
            return response.GetString() ?? "";
        }

        return "";
    }

    private async Task ServeStaticAsync(Stream stream, string path)
    {
        var relative = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(relative))
        {
            relative = Path.Combine("web", "index.html");
        }

        var fullPath = Path.GetFullPath(Path.Combine(WorkspaceRoot, relative));
        if (!IsUnderRoot(fullPath, WorkspaceRoot))
        {
            await WriteTextAsync(stream, 403, "Forbidden");
            return;
        }

        if (Directory.Exists(fullPath))
        {
            fullPath = Path.Combine(fullPath, "index.html");
        }

        if (!File.Exists(fullPath))
        {
            await WriteTextAsync(stream, 404, "Not found");
            return;
        }

        var contentType = ContentTypes.TryGetValue(Path.GetExtension(fullPath), out var value)
            ? value
            : "application/octet-stream";
        await WriteFileAsync(stream, fullPath, contentType);
    }

    private static bool IsUnderRoot(string path, string root)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || path.Equals(root, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteFileAsync(Stream stream, string path, string contentType)
    {
        using var file = File.OpenRead(path);
        await WriteHeaderAsync(stream, 200, contentType, file.Length);
        await file.CopyToAsync(stream);
    }

    private static async Task WriteTextAsync(Stream stream, int statusCode, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await WriteHeaderAsync(stream, statusCode, "text/plain; charset=utf-8", bytes.Length);
        await stream.WriteAsync(bytes);
    }

    private static async Task WriteJsonAsync(Stream stream, object payload)
        => await WriteJsonAsync(stream, 200, payload);

    private static async Task WriteJsonAsync(Stream stream, int statusCode, object payload)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        await WriteHeaderAsync(stream, statusCode, "application/json; charset=utf-8", bytes.Length);
        await stream.WriteAsync(bytes);
    }

    private static async Task WriteHeaderAsync(Stream stream, int statusCode, string contentType, long contentLength)
    {
        var reason = statusCode switch
        {
            200 => "OK",
            400 => "Bad Request",
            403 => "Forbidden",
            404 => "Not Found",
            405 => "Method Not Allowed",
            502 => "Bad Gateway",
            503 => "Service Unavailable",
            504 => "Gateway Timeout",
            _ => "Server Error"
        };
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {statusCode} {reason}\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Length: {contentLength}\r\n" +
            "Cache-Control: no-store\r\n" +
            "Connection: close\r\n" +
            "\r\n");
        await stream.WriteAsync(header);
    }
}

internal sealed record LauncherSettings(string ProjectPath, string Roots, string OutputPath);

internal readonly record struct SourceLocation(string File, int Line, int Priority);

internal sealed class AiRequestException : Exception
{
    public AiRequestException(string message)
        : base(message)
    {
    }
}

internal static class ErrorLog
{
    public static void Write(Exception exception)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "launcher-error.log");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{exception}\n\n");
        }
        catch
        {
            // Last-resort logging should never crash the launcher.
        }
    }
}
