# Unity Code Graph

[Korean README](README.ko.md)

Unity Code Graph scans Unity/C# source files and turns type relationships,
method calls, Unity-specific references, and likely system clusters into an
interactive graph viewer.

It is designed as a local-first inspection tool: no AI, no project upload, and
no Unity editor integration required. The analyzer reads `.cs` files with
Roslyn syntax trees and emits JSON that the included web viewer can load.

![Launcher](docs/screenshots/launcher.png)

![Web Viewer](docs/screenshots/web-viewer.png)

## Features

- Scan local Unity projects or plain C# folders.
- Generate a JSON graph from classes, structs, records, interfaces, and enums.
- Track type relationships such as inheritance, fields, properties, parameters,
  local variables, object creation, casts, type checks, and attributes.
- Detect Unity-style references such as `GetComponent<T>()`, `AddComponent<T>()`,
  `FindObjectOfType<T>()`, and `ScriptableObject.CreateInstance<T>()`.
- Build a method call graph for calls that can be resolved from local syntax.
- Show system clusters, system reports, flow traces, and per-node code call
  summaries in the web viewer.
- Save graph layout locally, then export/import layout JSON between browsers or
  machines.
- Use the WebView2 launcher to generate graphs, watch for changes, and open the
  canvas with the latest output file.

## Requirements

- Windows
- .NET 9 SDK
- WebView2 Runtime for the launcher
- Node.js for the shortcut JavaScript syntax checks and optional static server

## Quick Start

Build everything and run syntax checks:

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
- Use `Export Layout` and `Import Layout` to move saved positions, filters, view
  mode, and zoom state between browsers or machines.
- Select a node to see details, related examples, code call summaries, and flow
  traces.

## Build And Publish

Quick Debug build:

```powershell
.\build.bat
```

Release build checks:

```powershell
.\build.bat -Release
```

Publish the Windows bundle and zip:

```powershell
.\build.bat -Release -Publish -Zip
```

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

- `inherits`
- `implements`
- `has_attribute`
- `member_attribute`
- `attribute_type_argument`
- `type_constraint`
- `has_field_type`
- `has_property_type`
- `has_event_type`
- `returns`
- `accepts_parameter`
- `uses_local_type`
- `creates`
- `typeof`
- `casts_to`
- `type_check`
- `calls_member`
- `unity_get_component`
- `unity_try_get_component`
- `unity_add_component`
- `unity_find_object`
- `unity_create_scriptable_object`

## Notes

This project intentionally avoids AI for the current analysis pass. The graph is
based on mechanical extraction from C# syntax, which keeps the output local,
repeatable, and explainable.
