# Lumos Desktop

Windows-first AI-native video editor inspired by Palmier Pro. This repository is C#/.NET 9 with Avalonia UI, not Swift/macOS.

Lumos should become the Windows implementation path for a Palmier-style editor: real timeline editing first, then MCP and AI workflows on top of the same editor state.

## Build

```bash
dotnet build src/Lumos.sln
dotnet test src/Lumos.sln
dotnet run --project src/Lumos.Desktop/Lumos.Desktop.csproj
```

If `Lumos.Desktop.exe` is locked during build, check for running `Lumos.Desktop` processes before assuming the build is broken.

## Product Priorities

Follow the roadmap in `readme.md`.

Current priority order:

1. Stabilize the prototype.
2. Make the core editor MVP real: import, play, edit, export.
3. Add durable project and media persistence.
4. Polish timeline and preview behavior.
5. Expand MCP parity.
6. Add AI-assisted editing.
7. Add generative media workflows.
8. Productize for Windows.

Do not jump to AI or browser/workspace features before the editor core is stable.

## Architecture

Main layers:

- `src/Lumos.Domain`: timeline, tracks, clips, effects, keyframes, timeline math.
- `src/Lumos.Application`: editor store, commands, asset management, application state.
- `src/Lumos.Media`: playback, frame provider, decode pipeline, seek, cache, audio utilities.
- `src/Lumos.Infrastructure`: FFmpeg/export, Skia compositing, thumbnails, effects.
- `src/Lumos.MCP`: tool definitions and tool dispatch.
- `src/Lumos.AI`: model clients, streaming parser, agent service, semantic search.
- `src/Lumos.Desktop`: Avalonia UI and view models.
- `src/Lumos.Tests`: xUnit coverage.

Keep behavior in the lowest practical layer. UI should call application/media services instead of duplicating timeline or media logic.

## Code Style

- Keep comments minimal. Only add one when the why is non-obvious.
- Do not leave breadcrumbs like `// removed old path` or long explanatory comment blocks.
- Prefer clear names and small methods over comments.
- Preserve the existing public API shape unless the change requires a contract update.
- Keep edits scoped to the request. Avoid unrelated refactors.
- Do not silently swallow exceptions in core editor/media code. Surface useful logs or errors.

## UI Guidelines

Lumos uses Avalonia XAML and MVVM.

- Preserve the current layout unless the task explicitly asks for redesign.
- Keep the app feeling like a focused Windows desktop editor, not a marketing page.
- Prefer view models and bindings over code-behind for new stateful UI.
- Code-behind is acceptable for event wiring and Avalonia interop already following the local pattern.
- Avoid adding new hardcoded styling values when a reusable resource or style can be introduced.
- Long-term target: create a Windows/Avalonia theme equivalent to Palmier Pro's `AppTheme` and migrate repeated colors, spacing, radii, font sizes, and opacity into it.
- Until that theme exists, keep visual changes conservative and consistent with the current UI.

## Media And Playback

Be careful in playback code. Repeated mistakes here make the app feel broken.

- Real video playback must use the frame provider/decode pipeline, not image-only decoding.
- Keep preview buffer sizes consistent between `VideoEngine`, compositor, and `WriteableBitmap`.
- Avoid per-frame allocations when a reusable bitmap or buffer is practical.
- Playback should not crash the app because one frame fails.
- Fix the root cause of black frames, seek stalls, or export mismatches rather than hiding errors.
- Export and preview should share the same compositing assumptions where possible.

## Commands And Timeline

All durable timeline mutations should go through commands.

- One user action should map to one undoable command where possible.
- MCP tools must validate inputs before mutating timeline state.
- Failed tool calls must not leave partial timeline mutations.
- Linked video/audio behavior should stay consistent across UI, commands, and MCP.
- Timeline math belongs in Domain/Application tests, not only UI behavior.

## MCP And AI

MCP is a core architecture goal, but it depends on the editor core.

- MCP tools should operate real project/editor state.
- Prefer explicit schemas and actionable errors.
- Do not invent AI-only edit paths that bypass the command system.
- In-app chat and external MCP clients should eventually share tool behavior.
- Keep AI optional. Lumos must remain useful with no API keys configured.

## Testing

Use tests according to risk:

- Timeline math and commands: unit tests.
- MCP tool dispatch and validation: unit tests.
- Media/compositing changes: focused regression tests when possible.
- UI-only changes: build plus targeted manual smoke test where automation is unavailable.

Before handing off a functional change, run at least:

```bash
dotnet build src/Lumos.sln
dotnet test src/Lumos.sln
```

If tests cannot be run, state why.

## Windows Product Notes

This is a Windows-first app.

- Prefer Windows-friendly paths and file handling.
- Future distribution should support `.lumos` project files, installer packaging, settings, crash logs, and file associations.
- Do not add macOS-only assumptions or Swift/AppKit/AVFoundation references to root instructions.

## Voice

Lumos should speak like a capable editor for creators: direct, technical, calm, and concise.

Use action-first language for prompts and state-first language for status. Avoid chatty, cute, or marketing-heavy product copy inside the app.
