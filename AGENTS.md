# Lumos Desktop

Windows-first AI-native video editor. C# .NET 9 with Avalonia UI. Windows 10/11 only, x64 only.

## Build

```bash
dotnet build src/Lumos.sln
dotnet test src/Lumos.sln
dotnet run --project src/Lumos.Desktop/Lumos.Desktop.csproj
```

If `Lumos.Desktop.exe` is locked during build, check for running `Lumos.Desktop` processes before assuming the build is broken.

## Code Style

- Keep comments minimal. Only write one when the *why* is non-obvious. Don't restate what the code does, don't narrate the current change, don't leave `// removed X` breadcrumbs. One short line max — no multi-line comment blocks or paragraph docstrings.
- Prefer clear names and small methods over comments.
- Preserve the existing public API shape unless the change requires a contract update.
- Keep edits scoped to the request. Avoid unrelated refactors.
- Do not silently swallow exceptions in core editor/media code. Surface useful logs or errors.

## Design System

All UI styling MUST use `LumosTheme` constants from `src/Lumos.Desktop/LumosTheme.cs`. Never use hardcoded hex/numeric values for:

- **Colors** → `LumosTheme.Color.*` (Bg, Text, Border, Accent, Track colors, etc.)
- **Spacing** → `LumosTheme.Spacing.*` (xxs through xxxl)
- **Font sizes** → `LumosTheme.FontSize.*` (xxs through display)
- **Font weights** → `LumosTheme.FontWeight.*` (normal, medium, semibold, bold)
- **Corner radii** → `LumosTheme.Radius.*` (xs through round)
- **Border widths** → `LumosTheme.BorderWidth.*` (hairline, thin, medium, thick)
- **Opacity** → `LumosTheme.Opacity.*` (subtle through solid)
- **Icon sizes** → `LumosTheme.IconSize.*` (xs through xxl)
- **Track dimensions** → `LumosTheme.Track.*`
- **Window defaults** → `LumosTheme.Window.*`

If a needed value doesn't exist in LumosTheme, add it there first — don't hardcode it.

XAML styles should still use inline hex values from `LumosTheme.Color.*` until a full Avalonia resource dictionary exists, but all code-behind must reference `LumosTheme` constants.

## Drag And Drop

In Avalonia, `DragDrop.AllowDrop` is an attached property set per-element. Unlike Palmier Pro's SwiftUI `.onDrop`, Avalonia does not shadow inner drop targets — events bubble up the visual tree via `RoutingStrategies.Bubble`.

Rule: **attach drop handlers with `AddHandler(DragDrop.DropEvent, handler)` on the parent container, then walk `e.Source` up via `StyledElement.Parent` to find the target element.** See `WireFolderDragDrop` in `MainWindow.axaml.cs` for the established pattern.

Do not use the `Drop="OnXxx"` XAML attribute syntax — Avalonia does not support attached event handlers in XAML. Always use code-behind `AddHandler`.

## Voice

Lumos should speak like a capable editor for creators: direct, technical, calm, and concise.

Use action-first language for prompts and state-first language for status. Avoid chatty, cute, or marketing-heavy product copy inside the app.
