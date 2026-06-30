# Project Agent Guide

Before answering architecture, code-flow, or feature ownership questions, read:

- `ai-context/index.md`

For feature-specific work, also read the matching file under:

- `ai-context/systems/*.md`

Treat `ai-context` as generated graph evidence, not as final truth. If behavior matters, inspect the referenced source files and methods directly.

If `ai-context-enhanced` exists, treat it as AI-written interpretation layered on top of `ai-context`. Prefer `ai-context` for evidence and use enhanced files only as reading guides.

Useful commands:

```powershell
.\build.bat
dotnet run --project .\UnityCodeGraph -- <project-path> --roots Scripts,Source --output code-graph.json
```

When graph data looks stale, regenerate the graph before relying on `ai-context`.
