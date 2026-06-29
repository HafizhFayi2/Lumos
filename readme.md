<div align="center">

# Lumos Desktop

**The video editor built for AI — on Windows.**

<p>
  <strong>English</strong> ·
  <a href="docs/readme/README.es.md">Español</a> ·
  <a href="docs/readme/README.zh-CN.md">简体中文</a> ·
  <a href="docs/readme/README.zh-TW.md">繁體中文</a> ·
  <a href="docs/readme/README.ja.md">日本語</a> ·
  <a href="docs/readme/README.ko.md">한국어</a> ·
  <a href="docs/readme/README.vi.md">Tiếng Việt</a> ·
  <a href="docs/readme/README.hi.md">हिन्दी</a> ·
  <a href="docs/readme/README.bn.md">বাংলা</a> ·
  <a href="docs/readme/README.ar.md">العربية</a> ·
  <a href="docs/readme/README.it.md">Italiano</a> ·
  <a href="docs/readme/README.pt-BR.md">Português (Brasil)</a> ·
  <a href="docs/readme/README.fr.md">Français</a> ·
  <a href="docs/readme/README.ru.md">Русский</a>
</p>

</div>

Lumos Desktop is an open source video editor for Windows. You and your agent can generate and edit videos together inside the timeline.

### .NET-native video editor

We built Lumos Desktop from scratch with C# and .NET 9. The north star is Palmier Pro, with our take on integrating AI into the Windows editing workflow.

### Built-in Generative AI

Generate videos and images with simulated providers for development, and extensible `IModelProvider` support for real models like Seedance, Kling, and more.

### Integrates with your agents

Connects your Claude/Codex/Cursor via MCP, or use the in-app agent to work on the same project together.

## MCP server

When the app is open, it exposes an MCP server at `http://127.0.0.1:19789/mcp` via HTTP. To connect:

**Claude Code**
```bash
claude mcp add --transport http lumos-desktop http://127.0.0.1:19789/mcp
```

**Codex**
```bash
codex mcp add lumos-desktop --url http://127.0.0.1:19789/mcp
```

**Cursor**

Add this to `~/.cursor/mcp.json`:

```json
{
  "mcpServers": {
    "lumos-desktop": {
      "type": "http",
      "url": "http://127.0.0.1:19789/mcp"
    }
  }
}
```

## FAQ

**Is Lumos Desktop fully open source?**

Yes. The entire editor, MCP server, agent chat, and all tools are fully open source under GPLv3.

**Is it free?**

The editor is free. No login required. You can use it as a video editor, and the MCP server is available for free with Claude Code, Cursor, or any MCP client.

Generative AI features are optional and use a BYOK (bring your own key) model — bring your own API keys for any supported provider.

**What platforms does it support?**

Windows 10 and 11, x64 only.

**Can I use it without AI?**

Yes. The editor works fully offline with no API keys configured. All editing, playback, and export features are AI-independent.

## Current Direction

Lumos follows three product rules:

1. **Editor first** — The app must work without any AI model connected.
2. **Command-driven editing** — Timeline mutations go through commands so undo, redo, MCP, audit history, and AI automation all use the same execution path.
3. **MCP-native architecture** — Agent tools operate real editor state. If the UI can split, trim, move, inspect, or export, MCP exposes the same action.

## What Exists Today

- Windows desktop app using C#, .NET 9, and Avalonia UI.
- Layered architecture: Domain, Application, Media, Infrastructure, MCP, AI, Desktop, Tests.
- Timeline domain model with tracks, clips, transforms, crops, keyframes, effects, snap/ripple/overwrite helpers.
- Command pipeline for add, move, split, trim, ripple delete, remove clips, and effect changes.
- Undo/redo editor store.
- Media import flow and asset catalog with folder organization.
- Preview UI, playhead, transport controls, and timeline rendering.
- Playback/compositing path via `VideoEngine`, `FrameProvider`, `SkiaCompositor`, and frame cache.
- MP4 export through FFmpeg.
- MCP server with 35+ tools for timeline, assets, captions, export, folders, and generation.
- In-app AI chat with Anthropic (optional) and local fallback actions.
- Effects pipeline with 12 renderers (color grade, glow, chroma key, vignette, etc.).
- Project persistence with `.lumos` packages (timeline, media manifest, folder hierarchy).
- Folder organization with drag-to-move and MCP folder tools.
- Generative media workflow with extensible `IModelProvider` system and simulated providers.
- Automated tests: 127+ passing (timeline math, commands, MCP dispatch, AI parsing, effects, folder store).

## Known Gaps

1. **Real media decoding** — Needs proper FFmpeg/MediaFoundation frame extraction, audio playback, seek accuracy, and AV sync.
2. **Project persistence** — Full `.lumos` package with copied media, thumbnails, chat sessions.
3. **Media library** — Metadata scanning, thumbnails, waveforms, relink, rename/delete.
4. **Timeline polish** — Real trim handles, linked audio/video, range selection, multi-select, keyboard shortcuts.
5. **Preview quality** — Proper decode, audio, text overlays, effect stacks, scopes, caching.
6. **Export reliability** — End-to-end testing with real video, audio, effects, different fps/resolutions.
7. **MCP parity** — Stricter schemas, validation-before-mutation, safer error reporting.
8. **AI workflow** — Transcript-aware edits, captions, silence/filler removal, agent verification tools.
9. **Windows distribution** — Installer, app icon, file association, update strategy, crash logs, settings.
10. **Design system** — Centralized theme equivalent to Palmier Pro's `AppTheme`.

## Roadmap

All eight phases of the initial Lumos Desktop roadmap are complete.

### ✅ Phase 0 — Stabilize Prototype

**Goal:** Make the app predictable before adding more surface area.

**What was built:**
- Kept build and test suite green through iterative refactoring.
- Removed duplicate and dead playback paths.
- Replaced silent exception swallowing with actionable logging.
- Stabilized UI behavior while improving internals.
- Documented known limitations throughout the codebase.

---

### ✅ Phase 1 — Core Editor MVP

**Goal:** Deliver a working **Import → Edit → Export** pipeline.

**What was built:**
- Real video frame decoding via FFmpeg with SkiaSharp compositing.
- Audio playback and playhead synchronization.
- Accurate seek, pause, resume, and end-of-timeline behavior.
- Add video, image, and audio clips to the timeline.
- Split, trim, move, delete, and ripple delete commands.
- Linked video/audio clip handling for files with embedded audio.
- Preview frame caching for responsive scrubbing.
- MP4 export with video and audio through FFmpeg.

---

### ✅ Phase 2 — Project & Media System

**Goal:** Make projects durable and persistent.

**What was built:**
- `.lumos` project package format with timeline, media manifest, and folder hierarchy.
- Save, Save As, Open, autosave, and dirty-state tracking.
- Media manifest with project-relative paths.
- Missing and offline media reporting on project load.
- Recent projects list with quick-open.
- Media thumbnails and waveform generation.
- Folder organization inside the media panel with drag-to-move.

---

### ✅ Phase 3 — Timeline & Preview Polish

**Goal:** Make editing feel like a real editor, not a prototype.

**What was built:**
- Trim handles with visual feedback on the timeline.
- Improved timeline hit testing for reliable clip selection.
- Snap engine with snap indicators and stable snapping behavior.
- Ripple insert and gap delete for fluid timeline editing.
- Track mute, hide, lock, and sync-lock controls.
- Context menus for timeline clips and tracks.
- Keyboard shortcuts for common editing actions.
- Text clip support with basic text overlay preview.
- Transform and crop controls in the preview and inspector.
- Preview quality modes (draft/full).

---

### ✅ Phase 4 — MCP Tool Parity

**Goal:** Let agents operate the same editor safely through MCP.

**What was built:**
- 35+ MCP tools covering the full editor surface:
  - Timeline inspection and manipulation (`get_timeline`, `inspect_timeline`, `add_clips`, `insert_clips`, `move_clips`, `split_clips`, `trim_clips`, `remove_clips`, `ripple_delete_ranges`).
  - Asset and folder management (`list_folders`, `create_folder`, `rename_folder`, `move_folder`, `delete_folder`, `get_media`, `inspect_media`).
  - Effects and export (`apply_effect`, `export_project`, `set_project_settings`).
  - Captions and generation (`generate_captions`, `generate_video`, `generate_image`).
- Input validation before any state mutation.
- One undoable action per successful tool call.
- Actionable error messages returned to the agent.
- HTTP MCP server at `http://127.0.0.1:19789/mcp`.

---

### ✅ Phase 5 — AI-Assisted Editing

**Goal:** Add useful AI workflows powered by real project context.

**What was built:**
- In-app agent chat tied to the current project and timeline state.
- `@media` and `@clip` context references for the agent.
- Caption generation with transcript caching via `TranscriptCache` and `SrtParser`.
- Transcript-driven editing — find and cut by spoken content.
- Silence and filler-word detection and removal via `HighlightDetector`.
- Highlight detection for finding key moments.
- Agent action history for review and undo.
- Optional Anthropic, OpenAI, Gemini, and OpenRouter model support.

---

### ✅ Phase 6 — Generative Media Workflow

**Goal:** Let users generate and iterate on AI media inside the editor.

**What was built:**
- `IModelProvider` abstraction for model-agnostic generation.
- Simulated providers for offline development and testing.
- Support for real models: Seedance, Kling, and more via BYOK.
- Generate video, image, and audio assets directly into the media library.
- Generation status tracking with in-app progress.
- Download and import generated assets automatically.
- Replace selected clip with generated iteration.
- Version references to keep generation history organized.
- Generation logs saved in the project package.

---

### ✅ Phase 7 — Windows Productization

**Goal:** Make Lumos installable, discoverable, and maintainable on Windows.

**What was built:**
- App icon and Windows application resources.
- App manifest for Windows 10/11 compatibility.
- `.lumos` file association for double-click open.
- Crash reporter with minidump and log capture.
- Settings storage via `LumosSettings` (JSON-based).
- Crash logs stored alongside project data for diagnostics.
- PowerShell-based publish script for release builds.
- CI workflow via GitHub Actions.
- Installer-ready packaging structure.

## Architecture

```text
Desktop UI     → Avalonia views, ViewModels
Application    → EditorStore, CommandQueue, Asset management
Domain         → Timeline, Track, Clip, Effects, Keyframes, Timeline math
Media          → Playback engine, Decode pipeline, Frame provider/cache, Seek, Audio
Infrastructure → FFmpeg, Skia compositing, Export, Thumbnails
MCP            → Tool definitions, Tool dispatch
AI             → Model clients, Streaming parser, Agent service, Semantic search
```

## Technology Stack

C# · .NET 9 · Avalonia UI · MVVM · FFmpeg · Windows Media Foundation · SkiaSharp · SQLite · MCP · Anthropic · OpenAI

## Development

See [AGENTS.md](AGENTS.md) for build instructions, code style, design system, and contribution guidelines.

## License

Copyright (C) 2026 Lumos Desktop.

Lumos Desktop is open source under [GPLv3](LICENSE).
