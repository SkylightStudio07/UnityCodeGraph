using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var requestLine = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(requestLine))
            {
                return;
            }

            string? line;
            do
            {
                line = await reader.ReadLineAsync();
            } while (!string.IsNullOrEmpty(line));

            var parts = requestLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !parts[0].Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                await WriteTextAsync(stream, 405, "Method Not Allowed");
                return;
            }

            var path = Uri.UnescapeDataString(new Uri(parts[1], UriKind.RelativeOrAbsolute).IsAbsoluteUri
                ? new Uri(parts[1]).AbsolutePath
                : parts[1].Split('?', 2)[0]);

            if (path.Equals("/graph/current.json", StringComparison.OrdinalIgnoreCase))
            {
                await ServeGraphAsync(stream);
                return;
            }

            await ServeStaticAsync(stream, path);
        }
        catch
        {
            if (stream.CanWrite)
            {
                await WriteTextAsync(stream, 500, "Server error");
            }
        }
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

    private static async Task WriteHeaderAsync(Stream stream, int statusCode, string contentType, long contentLength)
    {
        var reason = statusCode switch
        {
            200 => "OK",
            403 => "Forbidden",
            404 => "Not Found",
            405 => "Method Not Allowed",
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
