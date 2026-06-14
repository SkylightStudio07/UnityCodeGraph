<p align="center">
  <img src="docs/assets/ucg-mark.svg" width="118" alt="Unity Code Graph mark" />
</p>

<h1 align="center">Unity Code Graph</h1>

<p align="center">
  <strong>A local graph viewer for Unity/C# relationships, call flow, and optional AI walkthroughs.</strong>
  <br />
  <em>Works without an AI key; adds reading guides only when you opt in.</em>
</p>

<p align="center">
  <a href="README.md">한국어</a>
  ·
  <a href="README.en.md"><strong>English</strong></a>
</p>

<p align="center">
  <img alt=".NET 9" src="https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white" />
  <img alt="Windows" src="https://img.shields.io/badge/Windows-Launcher-0078D4?logo=windows&logoColor=white" />
  <img alt="Unity" src="https://img.shields.io/badge/Unity-C%23%20Graph-111111?logo=unity&logoColor=white" />
  <img alt="Roslyn" src="https://img.shields.io/badge/Roslyn-Syntax%20Analysis-8dccff" />
  <img alt="Local first" src="https://img.shields.io/badge/Local--first-No%20Upload-24754f" />
  <img alt="Preview" src="https://img.shields.io/badge/status-preview-f0b429" />
</p>

<p align="center">
  <a href="#quick-start">Quick Start</a>
  ·
  <a href="#launcher">Launcher</a>
  ·
  <a href="#web-viewer">Web Viewer</a>
  ·
  <a href="#ai-walkthrough">AI Walkthrough</a>
  ·
  <a href="#build-and-publish">Build</a>
  ·
  <a href="#verify-analysis">Verify</a>
</p>

---

## Preview

![Launcher](docs/screenshots/launcher.png)

![Web Viewer](docs/screenshots/web-viewer.png)

![AI Walkthrough](docs/screenshots/web-viewer-ai-walkthrough3.png)

## What Is This?

Unity Code Graph scans Unity projects or plain C# folders and emits a JSON graph
of type relationships and method calls. The included web viewer can load that
graph, let you move nodes around, inspect systems, and export/import the layout.

The core analysis pass does not require AI. It extracts mechanically verifiable
relationships from C# syntax so the graph stays local, repeatable, and
explainable. AI features are optional and only act as a reading assistant for
already extracted graph data.

## Features

| Area | Features |
| --- | --- |
| Code scan | Local Unity projects, plain C# folders, public Git repositories |
| Type graph | Classes, structs, records, interfaces, and enums |
| Relationships | Inheritance, implementation, fields, properties, parameters, locals, creation, casts, type checks, attributes |
| Unity patterns | `GetComponent<T>()`, `AddComponent<T>()`, `FindObjectOfType<T>()`, `CreateInstance<T>()` |
| Call graph | Syntax-resolved method calls and type-level call summaries |
| Web viewer | System clusters, system reports, flow traces, Code Calls, auto reload, first-run guidance |
| AI assistant | Node summaries, system summaries, code reading walkthroughs, evidence edges, graph-jump chips |
| Layout | Saved positions, `Export Layout`, `Import Layout` |
| Launcher | WebView2 GUI, recent projects, watch mode, built-in local server, AI provider proxy |

## Requirements

- Windows
- .NET 9 SDK
- WebView2 Runtime
- Node.js for shortcut JavaScript checks and optional static serving

## Quick Start

Build everything and run JavaScript syntax checks:

```powershell
.\build.bat
```

Generate a graph from a Unity project:

```powershell
dotnet run --project .\UnityCodeGraph -- <path-to-unity-project> --roots Scripts,Source --output graph.json
```

Generate from a direct code folder:

```powershell
dotnet run --project .\UnityCodeGraph -- <path-to-unity-project>\Assets\Scripts --output graph.json
```

Watch a project and regenerate when `.cs` files change:

```powershell
dotnet run --project .\UnityCodeGraph -- <path-to-unity-project> --roots Scripts,Source --watch --output graph.json
```

## Launcher

Run the launcher from source:

```powershell
dotnet run --project .\UnityCodeGraph.Launcher
```

The launcher lets you:

- Choose a Unity project folder.
- Clone a public Git repository.
- Choose code folder names such as `Scripts,Source`.
- Generate once or start watch mode.
- Reopen recent projects.
- Open the graph canvas in your browser.

`Open Canvas` starts the launcher's built-in local static server. If the
configured `Output JSON` file exists, the canvas loads it automatically.

## Web Viewer

The easiest way to open the viewer is through the launcher's `Open Canvas`
button.

You can also serve the workspace root manually:

```powershell
dotnet run --project .\UnityCodeGraph -- .\samples\MiniUnityStyle --output .\samples\mini-graph.json
node .\tools\static-server.mjs 5173 .
```

Then open:

```text
http://localhost:5173/web/
```

In the viewer:

- Use `Load JSON` to open a generated graph file.
- Use `Type View` for type-level inspection.
- Use `System View` to collapse types into system cards.
- Use `Pin View` to keep a selected relationship view while moving nodes.
- Use `Auto Reload` to pick up graph JSON updates from launcher watch mode.
- Use `Export Layout` and `Import Layout` to move saved positions, filters, view
  mode, and zoom state between browsers or machines.
- Select a node to see details, examples, code call summaries, and flow traces.

## AI Walkthrough

![AI Walkthrough](docs/screenshots/web-viewer-ai-walkthrough3.png)

AI is optional. If no API key is configured, or if a request fails, graph
generation, the web viewer, layout import/export, and auto reload continue to
work normally.

How to use it:

1. Open the web viewer from the launcher with `Open Canvas`.
2. Click `AI` in the top-right toolbar and choose a provider, base URL, model,
   and API key.
3. Enable `Remember API key on this Windows user profile` and click `Save AI`
   if you want the launcher to remember the key.
4. Select a node or a system from the left `SYSTEMS` list, then run
   `AI Walkthrough`.
5. In the walkthrough panel, click the blue chips under `Reading Path` to jump
   to the referenced node or edge on the graph.

Supported providers:

- OpenAI Responses API
- OpenRouter / OpenAI-compatible Chat Completions
- DeepSeek Chat Completions
- Ollama local chat API
- Vertex AI has a settings slot, but request support is still planned

API keys are not stored in browser localStorage, graph JSON, or layout JSON.
When key saving is enabled, the launcher writes
`AppData\Local\UnityCodeGraph\ai-settings.json` with the key encrypted through
Windows DPAPI for the current user profile.

## Build And Publish

Quick Debug build:

```powershell
.\build.bat
```

If `dist\UnityCodeGraph-win-x64\` already exists, the latest web viewer files
are synced there too. Use `-Publish` when you need to rebuild the packaged exe.

Release build checks:

```powershell
.\build.bat -Release
```

Publish the Windows bundle and zip:

```powershell
.\build.bat -Release -Publish -Zip
```

Publishing cannot overwrite a running launcher executable. Close
`UnityCodeGraphLauncher.exe` first if the publish step reports a locked file.

The publish output is:

```text
dist\UnityCodeGraph-win-x64\
dist\UnityCodeGraph-win-x64.zip
```

Run the published launcher:

```powershell
.\dist\UnityCodeGraph-win-x64\UnityCodeGraphLauncher.exe
```

Run the published CLI:

```powershell
.\dist\UnityCodeGraph-win-x64\UnityCodeGraph.exe <path-to-unity-project> --roots Scripts,Source --output graph.json
```

Create a larger self-contained package that does not require a matching .NET
runtime on the target machine:

```powershell
.\build.bat -Release -Publish -Zip -SelfContained
```

## Verify Analysis

Run the parser regression fixture:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\verify-analysis.ps1
```

The fixture checks using-aware type resolution, type constraints, Unity generic
calls, casts and type checks, static calls, method call edges, and false-positive
avoidance for duplicate type names.

## Extracted Relationships

```text
inherits                         implements
has_attribute                    member_attribute
attribute_type_argument          type_constraint
has_field_type                   has_property_type
has_event_type                   returns
accepts_parameter                uses_local_type
creates                          typeof
casts_to                         type_check
calls_member                     unity_get_component
unity_try_get_component          unity_add_component
unity_find_object                unity_create_scriptable_object
```

## Status

This project is currently in preview. The analyzer is fast and repeatable
because it is based on static syntax extraction, but it is not yet a full
compiler semantic-model resolver.
