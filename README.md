# Unity C# Code Graph

First-pass parser for visualizing relationships between Unity/C# source files.

The current MVP scans `.cs` files and emits a JSON graph with type nodes and
relationships that can later be consumed by a web canvas.

## Run

```powershell
dotnet run --project .\UnityCodeGraph -- <path-to-unity-project-or-folder> --output graph.json
```

To scan only common Unity code folders under a project root:

```powershell
dotnet run --project .\UnityCodeGraph -- <path-to-unity-project> --roots Scripts,Source --output graph.json
```

You can also pass the code folder directly:

```powershell
dotnet run --project .\UnityCodeGraph -- <path-to-unity-project>\Assets\Scripts --output graph.json
```

For live Unity development, keep the parser running and regenerate the graph
whenever `.cs` files are added, changed, deleted, or renamed:

```powershell
dotnet run --project .\UnityCodeGraph -- <path-to-unity-project> --roots Scripts,Source --watch --output graph.json
```

## Build an Exe

Publish a Windows executable:

```powershell
.\tools\publish-win.ps1
```

Run the WebView2 GUI launcher:

```powershell
.\dist\UnityCodeGraph-win-x64\UnityCodeGraphLauncher.exe
```

The launcher uses an HTML/CSS interface hosted in WebView2. It lets you choose a
project folder, paste a Git repository URL, generate once, start/stop watch
mode, or open the web canvas.

`Open Canvas` starts the local static server and opens `http://127.0.0.1:5173/web/`
in your default browser. Use `Load JSON` in the canvas to choose the graph file
from the launcher's `Output JSON` path.

Run the CLI directly:

```powershell
.\dist\UnityCodeGraph-win-x64\UnityCodeGraph.exe <path-to-unity-project> --roots Scripts,Source --output graph.json
```

If you double-click the executable with no arguments, it starts an interactive
watch mode. Paste the Unity project path, confirm the code folder names, and it
will keep running until you press `Ctrl+C`.

Command-line watch mode:

```powershell
.\dist\UnityCodeGraph-win-x64\UnityCodeGraph.exe <path-to-unity-project> --roots Scripts,Source --watch --output graph.json
```

For a larger executable that does not require a matching .NET runtime on the
target machine:

```powershell
.\tools\publish-win.ps1 -SelfContained
```

## Web Canvas

Generate a graph, then serve the workspace root and open `/web/`.

```powershell
dotnet run --project .\UnityCodeGraph -- .\samples\MiniUnityStyle --output .\samples\mini-graph.json
node .\tools\static-server.mjs 5173 .
```

Open `http://localhost:5173/web/`.

The canvas can also load any generated graph file through the `Load JSON`
button.

The left `Systems` panel shows first-pass system clusters such as card, battle,
map generation, and UI areas. Selecting a system filters the canvas to that
cluster and shows a local `System Report` with likely role, major types, entry
candidates, likely static flows, and external touchpoints.

## Extracted Relationships

- `inherits`
- `implements`
- `has_attribute`
- `member_attribute`
- `attribute_type_argument`
- `has_field_type`
- `has_property_type`
- `has_event_type`
- `returns`
- `accepts_parameter`
- `uses_local_type`
- `creates`
- `typeof`
- `calls_member`
- `unity_get_component`
- `unity_try_get_component`
- `unity_add_component`
- `unity_find_object`
- `unity_create_scriptable_object`

## Notes

This version intentionally avoids AI and focuses on mechanical extraction. It
uses Roslyn syntax trees from the installed .NET SDK, so it does not need NuGet
packages for the first pass.
