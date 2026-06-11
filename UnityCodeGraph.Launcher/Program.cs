using System.Diagnostics;
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
    private Process? _canvasServerProcess;

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

        var serverPath = Path.Combine(workspaceRoot, "tools", "static-server.mjs");
        await EnsureCanvasServerAsync(serverPath, workspaceRoot);

        var url = "http://127.0.0.1:5173/web/";
        Log($"Opening canvas: {url}");
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    private async Task EnsureCanvasServerAsync(string serverPath, string workspaceRoot)
    {
        if (_canvasServerProcess is { HasExited: false } || await IsCanvasServerAvailableAsync())
        {
            return;
        }

        var nodePath = FindNodeExecutable();
        if (nodePath is null)
        {
            Log("Node.js was not found. Install Node.js or run the static server manually.");
            return;
        }

        Log("Starting canvas server.");
        var process = CreateProcess(nodePath, $"\"{serverPath}\" 5173 \"{workspaceRoot}\"");
        process.StartInfo.WorkingDirectory = workspaceRoot;
        process.EnableRaisingEvents = true;
        process.OutputDataReceived += (_, eventArgs) => Log(eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) => Log(eventArgs.Data);
        process.Exited += (_, _) => BeginInvoke(new Action(() =>
        {
            Log("Canvas server stopped.");
            _canvasServerProcess?.Dispose();
            _canvasServerProcess = null;
        }));
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _canvasServerProcess = process;

        for (var i = 0; i < 20; i++)
        {
            if (await IsCanvasServerAvailableAsync())
            {
                return;
            }

            await Task.Delay(150);
        }
    }

    private static async Task<bool> IsCanvasServerAvailableAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(500) };
            using var response = await client.GetAsync("http://127.0.0.1:5173/web/");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
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
        if (_canvasServerProcess is null)
        {
            return;
        }

        try
        {
            if (!_canvasServerProcess.HasExited)
            {
                _canvasServerProcess.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Convenience server shutdown can race with app close.
        }
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
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "UnityCodeGraph", "bin", "Debug", "net10.0", "UnityCodeGraph.exe")),
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
                var serverPath = Path.Combine(directory.FullName, "tools", "static-server.mjs");
                if (File.Exists(webPath) && File.Exists(serverPath))
                {
                    return directory.FullName;
                }
            }
        }

        return null;
    }

    private static string? FindNodeExecutable()
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim('"'), "node.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe");
        return File.Exists(defaultPath) ? defaultPath : null;
    }

    private static string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
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
