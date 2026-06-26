# Lumos Desktop

**AI-native video editor for Windows.**

Lumos Desktop is a Windows-first video editor inspired by Palmier Pro: a timeline editor that humans can use directly, while AI agents can operate the same project through MCP tools.

The goal is not to bolt AI onto a weak editor. The goal is to build a usable video editor first, then expose the editor's real project, media, timeline, preview, and export systems to agents.

Palmier Pro is the reference concept. Lumos is the Windows implementation path.

---

## Current Direction

Lumos follows three product rules:

1. **Editor first**
   The app must work without any AI model connected.

2. **Command-driven editing**
   Timeline mutations should go through commands so undo, redo, MCP, audit history, and AI automation all use the same execution path.

3. **MCP-native architecture**
   Agent tools should operate real editor state. If the UI can split, trim, move, inspect, or export, MCP should eventually expose the same action safely.

---

## What Exists Today

This repository already contains the early foundation:

- Windows desktop app using C#, .NET 9, and Avalonia UI.
- Layered projects for Domain, Application, Media, Infrastructure, MCP, AI, Desktop, and Tests.
- Timeline domain model with tracks, clips, transforms, crops, keyframes, effects, snap/ripple/overwrite helpers.
- Command pipeline for add, move, split, trim, ripple delete, remove clips, and effect changes.
- Undo/redo-oriented editor store.
- Media import flow and asset catalog.
- Preview UI, playhead, transport controls, and timeline rendering.
- Playback/compositing path using `VideoEngine`, `FrameProvider`, `SkiaCompositor`, and frame cache.
- MP4 export path through FFmpeg.
- MCP server foundation with timeline, asset, caption, and export tools.
- In-app AI chat foundation using Anthropic when configured, plus local fallback actions.
- Basic effects/renderers and inspector surfaces.
- Automated tests for timeline math, command behavior, MCP dispatch, AI parsing, and rendering smoke checks.

This is enough to validate the architecture. It is not yet enough to call the app a production video editor.

---

## Known Gaps

These are the main gaps before Lumos feels like a real Palmier-style Windows editor:

1. **Real media decoding**
   The playback pipeline exists, but the decode layer still needs proper FFmpeg or Windows Media Foundation frame extraction for real video frames, audio playback, seek accuracy, and AV sync.

2. **Project persistence**
   Lumos needs a real `.lumos` project package with timeline data, media manifest, copied/imported media, thumbnails, chat sessions, autosave, recent projects, and missing-media restore.

3. **Serious media library**
   The app needs folders, rename/delete, relink, generated media states, metadata scanning, video thumbnails, image thumbnails, and waveform generation.

4. **Timeline editing polish**
   The core commands exist, but the UI needs real trim handles, linked audio/video behavior, ripple insert/delete controls, range selection, snapping polish, multi-select, context menus, and keyboard shortcuts.

5. **Preview and compositing quality**
   The preview path needs proper decode, audio, text overlays, transforms, crop, effect stacks, scopes, quality modes, caching, and responsive scrubbing.

6. **Export reliability**
   Export must be tested end-to-end with real video, audio, images, text, effects, different fps values, and different resolutions.

7. **MCP parity**
   MCP tools need stricter schemas, validation-before-mutation, one undo step per tool call, inspect-media, inspect-timeline, insert clips, folders, project settings, and safer error reporting.

8. **AI workflow**
   The app needs timeline/media references, transcript-aware edits, captions, silence/filler removal, search media, generated asset tracking, and agent verification tools.

9. **Windows-native distribution**
   Lumos needs installer packaging, file association, app icon resources, update strategy, crash logs, settings storage, and optional code signing.

10. **Design system**
    The UI should move from scattered hardcoded values toward a Windows/Avalonia design system equivalent to Palmier Pro's `AppTheme`.

---

## Realistic Roadmap

### Phase 0 - Stabilize The Current Prototype

Goal: make the current app predictable before adding more surface area.

- Keep build and tests green.
- Remove duplicate/dead playback paths where possible.
- Replace silent exception swallowing with useful logs.
- Keep UI behavior stable while improving internals.
- Document known limitations clearly.

Exit criteria:

- `dotnet build src/Lumos.sln` passes.
- `dotnet test src/Lumos.sln` passes.
- App starts, imports a media file, adds it to the timeline, and does not crash on play.

---

### Phase 1 - Core Editor MVP

Goal: deliver the README target: **Import -> Edit -> Export**.

- Real video frame decoding through FFmpeg or Windows Media Foundation.
- Audio playback and playhead sync.
- Accurate seek, pause, resume, and end-of-timeline behavior.
- Add video/image/audio to timeline.
- Split, trim, move, delete, ripple delete.
- Linked video/audio clips for video files with audio.
- Basic preview caching.
- Export MP4 with video and audio.

Exit criteria:

- A user can import a real video, place it on the timeline, play it, cut it, trim it, delete clips, and export a playable MP4.
- The app remains useful with no AI key configured.

---

### Phase 2 - Project And Media System

Goal: make projects durable.

- `.lumos` project package.
- Save, Save As, Open, autosave, and dirty state.
- Media manifest and project media folder.
- Restore media from project package.
- Missing/offline media reporting.
- Recent projects.
- Media thumbnails and waveform generation.
- Folder organization inside the media panel.

Exit criteria:

- Closing and reopening a project restores timeline, media, thumbnails, and missing-media state.
- Imported project media survives path changes when copied into the project package.

---

### Phase 3 - Timeline And Preview Polish

Goal: make editing feel like an editor, not a demo.

- Trim handles.
- Better timeline hit testing.
- Multi-select and range select.
- Snap indicator and stable snapping behavior.
- Ripple insert and gap delete.
- Track mute, hide, lock, and sync-lock.
- Context menus.
- Keyboard shortcuts.
- Text clips and basic text overlay preview.
- Transform and crop controls in preview/inspector.
- Preview quality modes.

Exit criteria:

- Common editing tasks can be done mostly from the timeline without needing AI or debug-style controls.
- Preview updates reliably during scrubbing and timeline edits.

---

### Phase 4 - MCP Tool Parity

Goal: let agents operate the same editor safely.

- `get_timeline`
- `get_media`
- `inspect_timeline`
- `inspect_media`
- `add_clips`
- `insert_clips`
- `move_clips`
- `split_clips`
- `trim_clips`
- `remove_clips`
- `ripple_delete_ranges`
- `apply_effect`
- `export_project`
- `set_project_settings`
- Folder/media tools: list, create, rename, move, delete.

Tool rules:

- Validate all inputs before mutating state.
- Make each successful tool call one undoable action.
- Return actionable errors.
- Never leave partial timeline mutations after a failed tool call.

Exit criteria:

- Claude, Codex, Cursor, or another MCP client can inspect a project, add media, cut clips, verify the timeline, and export without using the UI.

---

### Phase 5 - AI-Assisted Editing

Goal: add useful AI workflows after the editor is stable.

- In-app agent context tied to the current project.
- `@media`, `@clip`, and timeline range references.
- Captions and transcript cache.
- Transcript-driven edits.
- Silence and filler-word removal.
- Highlight detection.
- Visual and spoken media search.
- Inspect timeline frames for agent verification.
- Agent action history.

Exit criteria:

- A user can ask the agent to find a moment, cut a range, create captions, remove silence, and verify the edit using real project context.

---

### Phase 6 - Generative Media Workflow

Goal: approach the Palmier Pro creative loop.

- Model catalog abstraction.
- BYOK/provider configuration.
- Generate image, video, audio, and upscale media.
- Download/import generated assets into the media library.
- Track generation status.
- Save generation logs in the project package.
- Replace selected clip with a generated iteration.
- Keep references and versions organized.

Exit criteria:

- A user can generate or import AI media, place it in the timeline, iterate versions, and keep the project organized without leaving Lumos.

---

### Phase 7 - Windows Productization

Goal: make Lumos installable and maintainable.

- App icon and Windows resources.
- Installer or packaged release.
- `.lumos` file association.
- Crash logging.
- Settings storage.
- Update strategy.
- Optional telemetry hooks.
- CI release workflow.
- Code signing path.

Exit criteria:

- A non-developer can install Lumos, open a project file, edit media, export video, and report useful diagnostics when something fails.

---

## Architecture

```text
Desktop UI
  Avalonia views
  ViewModels
  Timeline and preview interaction

Application
  EditorStore
  CommandQueue
  Timeline commands
  Asset management

Domain
  Timeline
  Track
  Clip
  Effects
  Keyframes
  Snap/ripple/overwrite math

Media
  Playback engine
  Decode pipeline
  Frame provider
  Frame cache
  Seek controller
  Audio analysis/mixing

Infrastructure
  FFmpeg integration
  Skia compositing
  Export
  Thumbnails
  File system integration

MCP
  Tool definitions
  Tool dispatch
  Agent-safe editor operations

AI
  Model clients
  Streaming parser
  Agent service
  Semantic search
```

---

## Technology Stack

- C#
- .NET 9
- Avalonia UI
- MVVM
- FFmpeg
- Windows Media Foundation
- SkiaSharp
- SQLite
- MCP
- Anthropic, OpenAI, Gemini, OpenRouter, and local models over time

---

## Build

```bash
dotnet build src/Lumos.sln
dotnet test src/Lumos.sln
dotnet run --project src/Lumos.Desktop/Lumos.Desktop.csproj
```

---

## Product North Star

Lumos should become a Windows-native video editor where:

- Humans can edit directly in the UI.
- Agents can inspect and edit through MCP.
- Media, timeline, transcript, generated assets, and project history live in one place.
- AI helps with repetitive production work without replacing the editor.

The short-term target is simple:

**Build a stable Windows editor first. Then make it AI-native.**

---

## License

License selection is still under evaluation.
