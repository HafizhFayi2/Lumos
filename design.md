## Overview

This is a **dark-canvas, sky-blue accent** design system for a native video-editing application (in the spirit of Lumos Pro / CapCut Desktop / DaVinci Resolve). The base atmosphere is a **near-black blue-tinted canvas** (`{colors.canvas}` — #0D1117) — a true editing-room dark theme so footage, thumbnails, and waveforms stay the visual focus, not the chrome around them.

Brand voltage comes from a **five-step sky-blue ramp**, lifted directly from the reference palette:

| Step | Hex | Role |
|---|---|---|
| Blue 600 | `#2986F6` | Primary actions, active states, brand mark |
| Blue 400 | `#4FC3F7` | Hover / secondary accent, playhead, live indicators |
| Blue 300 | `#81D4FA` | Tertiary accent, tags, selection outlines |
| Blue 200 | `#B3E5FC` | Soft accent, track highlight, disabled-but-visible states |
| Blue 100 | `#E1F5FE` | Palest tint, on-dark emphasis text, subtle glows |

The ramp goes from saturated/deep (interactive, "do this now") to pale/airy (ambient, "this is here"). No coral, no warm cream — the whole system runs cool blue against near-black, which is the standard grammar of professional NLE software (Premiere, Resolve, Final Cut) so the interface reads as a serious creative tool rather than a marketing site.

The system has three surface depths that stack on top of each other:
1. **App canvas** (`{colors.canvas}`) — outermost background, viewport letterboxing, timeline ruler track
2. **Panel surface** (`{colors.surface-card}`) — media library, inspector, chat/agent panel backgrounds
3. **Elevated surface** (`{colors.surface-elevated}`) — modals, dropdown menus, popovers, the floating "Generate" panel

**Key Characteristics:**
- Near-black blue canvas (`{colors.canvas}` — #0D1117) with off-white blue-tinted text (`{colors.ink}` — #F4F8FC). Dark theme is the default and only theme — editors live in dark rooms.
- Primary action color is `{colors.primary}` (#2986F6) — used on the Export button, Generate button, active timeline tool, and the brand mark. Scarce on individual elements, but it's the one color your eye should always find first.
- Sans-serif UI typeface throughout (no display serif) — this is a tool, not an editorial page. Density and legibility at small sizes matter more than character.
- Timeline tracks and waveforms use the blue ramp for state, not for decoration: selected clip = Blue 600 outline, playhead = Blue 400, hover scrub = Blue 300, muted/disabled track = Blue 200 at low opacity.
- Border radius is small and consistent — this is dense utility UI, not a marketing card layout. `{rounded.sm}` (6px) dominates; nothing goes past `{rounded.lg}` (12px) except the onboarding/empty-state illustrations.
- Spacing is tighter than a marketing site (`{spacing.section}` reduced to 32px) because the goal is maximum visible timeline/preview real estate, not whitespace pacing.

## Colors

### Brand & Accent
- **Blue Primary** (`{colors.primary}` — #2986F6): The signature action blue. Export button, Generate button, primary tool selection, brand mark, focus rings.
- **Blue Primary Active** (`{colors.primary-active}` — #1E6FD1): Press/hover-darker variant — one step deeper than primary.
- **Blue Hover / Secondary** (`{colors.accent-secondary}` — #4FC3F7): Hover states on primary buttons, the playhead scrubber, "recording/live" pulse dots.
- **Blue Tertiary** (`{colors.accent-tertiary}` — #81D4FA): Tags, AI-generated-clip badges, selection marquee outline on the timeline.
- **Blue Soft** (`{colors.accent-soft}` — #B3E5FC): Track-hover highlight, scrubber preview thumbnail border, disabled-but-visible toggle fill.
- **Blue Pale** (`{colors.accent-pale}` — #E1F5FE): Palest tint — used at low opacity as a glow behind the active panel tab, or as text-on-dark for the brand wordmark accent letter.
- **Primary Disabled** (`{colors.primary-disabled}` — #1B3A5C): Desaturated, darkened blue for disabled buttons on the dark canvas (not a light tint — disabled states stay dark-theme-correct).

### Surface
- **Canvas** (`{colors.canvas}` — #0D1117): App background, viewport letterbox bars, timeline ruler background.
- **Surface Soft** (`{colors.surface-soft}` — #11161D): Panel dividers, very-subtle band backgrounds (e.g. behind the transport bar).
- **Surface Card** (`{colors.surface-card}` — #161C26): Media library panel, inspector panel, AI-chat panel, clip thumbnail cards.
- **Surface Card Strong** (`{colors.surface-card-strong}` — #1C2430): Selected list row, active tab background, hovered clip card.
- **Surface Timeline Track** (`{colors.surface-timeline}` — #0A0E13): The track lanes themselves — darker than canvas so clips sitting on top visually lift off the lane.
- **Surface Elevated** (`{colors.surface-elevated}` — #212B38): Modals, context menus, the floating Generate/AI panel, dropdowns.
- **Surface Elevated Soft** (`{colors.surface-elevated-soft}` — #1A212B): Inner code/log panel backgrounds, console-style output (e.g. export log).
- **Hairline** (`{colors.hairline}` — #232C38): 1px border tone on dark surfaces — panel dividers, input borders, table row separators.
- **Hairline Soft** (`{colors.hairline-soft}` — #1A212B): Barely-visible divider used inside the same panel (e.g. between timeline track rows).

### Text
- **Ink** (`{colors.ink}` — #F4F8FC): Headlines, primary UI text, clip names. Off-white with a faint blue tint, never pure white (avoids glare against the dark canvas).
- **Body Strong** (`{colors.body-strong}` — #D7E3F0): Emphasized labels, panel section headers.
- **Body** (`{colors.body}` — #B7C4D4): Default running text — descriptions, tooltips, list item labels.
- **Muted** (`{colors.muted}` — #8593A6): Secondary labels, timecodes, breadcrumb-style paths in the media browser.
- **Muted Soft** (`{colors.muted-soft}` — #5C6A7C): Fine print, disabled text, frame-counter sub-labels.
- **On Primary** (`{colors.on-primary}` — #FFFFFF): Text/icons on filled blue buttons.
- **On Accent Pale** (`{colors.on-accent-pale}` — #0D1117): Text placed on top of the palest blue (#E1F5FE) fills — needs dark text for contrast.

### Semantic
- **Success** (`{colors.success}` — #4ADE80): Export complete, render success, "connected" MCP status dot.
- **Warning** (`{colors.warning}` — #FBBF24): Low credits warning, dropped-frame indicator.
- **Error** (`{colors.error}` — #F87171): Failed render, codec error, disconnected agent.
- **Recording / Live** (`{colors.live}` — #4FC3F7): Re-uses Blue Hover for "generating" / "recording" pulse states rather than introducing red, since blue is the brand's energy color.

## Typography

### Font Family
The system runs a **single neutral UI sans** — Inter (or SF Pro / system-ui as native substitute) — across every size, including headers. No serif display face: this is a dense professional tool, and a slab-serif headline would fight the timeline's information density. **JetBrains Mono** still handles any monospace need (timecodes, export logs, MCP/JSON payloads in the agent panel).

Fallback stack: `Inter, -apple-system, "SF Pro Display", "Segoe UI", Roboto, sans-serif` for all UI text; `"JetBrains Mono", "SF Mono", Menlo, monospace` for timecodes and logs.

### Hierarchy

| Token | Size | Weight | Line Height | Use |
|---|---|---|---|---|
| `{typography.title-xl}` | 24px | 600 | 1.2 | Modal titles, empty-state headlines |
| `{typography.title-lg}` | 18px | 600 | 1.3 | Panel section headers ("Media", "Inspector") |
| `{typography.title-md}` | 15px | 600 | 1.4 | Card titles, dialog labels |
| `{typography.title-sm}` | 13px | 600 | 1.4 | List group headers, tab labels |
| `{typography.body-md}` | 13px | 400 | 1.5 | Default UI text — descriptions, tooltips |
| `{typography.body-sm}` | 12px | 400 | 1.5 | Secondary list text, helper text |
| `{typography.caption}` | 11px | 500 | 1.3 | Badge labels, clip duration chips |
| `{typography.caption-uppercase}` | 10px | 600 | 1.3 | Track-type labels ("VIDEO", "AUDIO", "TEXT") |
| `{typography.timecode}` | 12px | 500 | 1.0 | Playhead time, in/out points — JetBrains Mono |
| `{typography.code}` | 12px | 400 | 1.6 | Export logs, MCP/JSON payloads — JetBrains Mono |
| `{typography.button}` | 13px | 500 | 1.0 | Standard button labels |
| `{typography.nav-link}` | 13px | 500 | 1.4 | Top-bar menu items |

### Principles
Weight carries hierarchy, not size — most of the UI sits between 11–15px, so jumps in size are small and jumps in weight (400 → 500 → 600) do the heavy lifting. No negative letter-spacing tricks; this typeface is read at small sizes for long sessions, so legibility wins over editorial flourish.

## Layout

### Spacing System
- **Base unit:** 4px.
- **Tokens:** `{spacing.xxs}` 4px · `{spacing.xs}` 8px · `{spacing.sm}` 12px · `{spacing.md}` 16px · `{spacing.lg}` 20px · `{spacing.xl}` 24px · `{spacing.xxl}` 32px · `{spacing.section}` 32px (much tighter than a marketing site — screen real estate goes to the preview/timeline, not whitespace).
- **Panel internal padding:** `{spacing.md}` (16px) for inspector/media panels; `{spacing.sm}` (12px) for compact list rows.
- **Timeline track height:** 56px default, 32px "compact" mode — not a spacing token but a core layout constant.

### Grid & Container
- **Workspace layout:** Three-pane: left = AI chat / media browser (≈280px), center = preview viewer (flexible) + timeline below it, right = inspector/properties (≈300px). No centered max-width container — the app fills the viewport.
- **Timeline:** Horizontal scroll, vertical track stack; ruler pinned to top of the timeline region.
- **Media grid:** Thumbnail grid in the library panel, 2–4 columns depending on panel width, square 1:1 or 16:9 thumbnail cards.

### Whitespace Philosophy
Density over breathing room. Every pixel of vertical space in the timeline and preview area is valuable; padding is just enough to keep touch/click targets comfortable, never decorative.

## Elevation & Depth

| Level | Treatment | Use |
|---|---|---|
| Flat | No shadow, no border | Canvas, timeline track lanes |
| Soft hairline | 1px `{colors.hairline}` border | Panel dividers, inputs, table rows |
| Panel card | `{colors.surface-card}` background, no shadow | Media library, inspector, chat panel |
| Elevated card | `{colors.surface-elevated}` background + soft shadow | Modals, dropdowns, the floating Generate panel |
| Glow accent | Blue Pale at 12–20% opacity, blurred | Active tab underglow, "AI is working" pulse around the chat panel border |

Depth in this system comes from **layered dark surfaces**, not from light/dark contrast like a marketing page — canvas → panel → elevated, each one a few percent lighter than the last, plus a 1px hairline to define the edge. The blue ramp supplies the only "color" depth cue: glows and outlines in Blue 400/300 signal "this is active/selected" against an otherwise monochrome dark stack.

### Decorative Depth
- Selected clips on the timeline get a 2px `{colors.primary}` outline (#2986F6) plus a 6%-opacity blue fill wash.
- The playhead is a thin `{colors.accent-secondary}` (#4FC3F7) vertical line with a small triangular handle at the ruler.
- "AI generating" states pulse a soft `{colors.accent-pale}` glow around the affected clip or panel border — same blue family, just the palest step, so it reads as "in progress" without introducing a new color.

## Shapes

### Border Radius Scale

| Token | Value | Use |
|---|---|---|
| `{rounded.xs}` | 3px | Chips, timecode badges |
| `{rounded.sm}` | 6px | Buttons, inputs, list rows — the dominant radius in this system |
| `{rounded.md}` | 8px | Cards, thumbnail tiles, dropdown menus |
| `{rounded.lg}` | 12px | Modals, the floating Generate panel |
| `{rounded.pill}` | 9999px | Status pills ("Connected", "Rendering…") |
| `{rounded.full}` | 50% | Avatar circles, circular icon buttons |

Radii stay small across the board — this is a precision tool, and large rounded corners would feel inconsistent with thin timelines and tight controls.

## Components

### Top Bar
**`top-bar`** — Dark bar pinned to the top, 44px tall, `{colors.canvas}` background, 1px `{colors.hairline}` bottom border. Carries the app mark + project name at left, transport controls (play/pause/loop) centered, Export/Share `{component.button-primary}` at right.

### Buttons
- **`button-primary`** — Background `{colors.primary}` (#2986F6), text `{colors.on-primary}` (white), `{typography.button}`, height 32px, padding 8px × 14px, rounded `{rounded.sm}`. Hover lightens to `{colors.accent-secondary}` (#4FC3F7); active/press darkens to `{colors.primary-active}` (#1E6FD1).
- **`button-secondary`** — Background `{colors.surface-card-strong}`, text `{colors.ink}`, 1px `{colors.hairline}` border, same sizing as primary.
- **`button-icon`** — 28px square icon button for the toolbar (cut, trim, split, zoom). Background transparent by default, `{colors.surface-card-strong}` on hover, `{colors.primary}` tint when the tool is active/selected.
- **`button-disabled`** — Background `{colors.primary-disabled}` (#1B3A5C), text `{colors.muted-soft}`, no hover state.

### Panels & Cards
- **`media-library-panel`** — Left panel, background `{colors.surface-card}`, grid of clip thumbnails with `{rounded.md}` corners, 8px gap, hover state lifts to `{colors.surface-card-strong}` with a `{colors.accent-tertiary}` 1px outline.
- **`inspector-panel`** — Right panel, background `{colors.surface-card}`, grouped property rows separated by `{colors.hairline-soft}`, section headers in `{typography.title-sm}`.
- **`ai-chat-panel`** — Left/dockable panel for the agent, background `{colors.surface-card}`, user messages right-aligned in a `{colors.primary}`-tinted bubble (10% opacity fill, `{colors.accent-tertiary}` text), assistant messages left-aligned in `{colors.surface-card-strong}`.
- **`generate-panel`** — Floating elevated card for AI media generation, background `{colors.surface-elevated}`, rounded `{rounded.lg}`, padding `{spacing.lg}` (20px), model picker dropdown + prompt field + a `{component.button-primary}` "Generate" button.

### Timeline
- **`timeline-track`** — Horizontal lane, background `{colors.surface-timeline}` (#0A0E13), 1px `{colors.hairline-soft}` separator between tracks, track-type label in `{typography.caption-uppercase}` at the left gutter.
- **`timeline-clip`** — Clip block on a track, background `{colors.surface-card-strong}`, rounded `{rounded.xs}`, waveform/thumbnail rendered inside at low opacity. Selected state: 2px `{colors.primary}` outline + 6% blue fill wash. AI-generated clips carry a small `{colors.accent-tertiary}` badge in the corner.
- **`playhead`** — 1px vertical line in `{colors.accent-secondary}` (#4FC3F7) spanning the full timeline height, with a small triangular grab handle at the ruler.
- **`timeline-ruler`** — Background `{colors.canvas}`, timecode ticks in `{typography.timecode}` / `{colors.muted}`.

### Inputs & Forms
- **`text-input`** — Background `{colors.surface-card-strong}`, text `{colors.ink}`, `{typography.body-md}`, rounded `{rounded.sm}`, height 32px, padding 8px × 10px, 1px `{colors.hairline}` border.
- **`text-input-focused`** — Border shifts to `{colors.primary}` with a 3px blue-at-15%-alpha outer ring.
- **`dropdown-select`** — Same base styling as text-input, with a chevron icon in `{colors.muted}`; open menu renders as `{component.context-menu}`.

### Tags / Badges / Status
- **`badge-ai`** — Small pill marking AI-generated content. Background `{colors.accent-tertiary}` at 16% opacity, text `{colors.accent-tertiary}` at full opacity, `{typography.caption}`, rounded `{rounded.pill}`, padding 2px × 8px.
- **`status-pill-connected`** — Background `{colors.success}` at 14% opacity, text `{colors.success}`, dot indicator, rounded `{rounded.pill}`. Used for MCP "Connected" status.
- **`status-pill-rendering`** — Background `{colors.accent-secondary}` at 14% opacity, text `{colors.accent-secondary}`, animated pulse dot.

### Menus & Overlays
- **`context-menu`** — Background `{colors.surface-elevated}`, rounded `{rounded.md}`, 1px `{colors.hairline}` border, soft shadow, row hover state `{colors.surface-card-strong}`.
- **`modal`** — Background `{colors.surface-elevated}`, rounded `{rounded.lg}`, padding `{spacing.xl}` (24px), backdrop is canvas at 70% opacity black overlay.

## Do's and Don'ts

### Do
- Keep the canvas dark always — this is an editing tool used for long sessions; a light theme would fight footage color judgment.
- Reserve `{colors.primary}` (#2986F6) for the one action you want noticed first per screen (Export, Generate, active tool).
- Use the blue ramp for *state*, not decoration: deeper blue = more active/selected, paler blue = more ambient/informational.
- Keep radii small and consistent (`{rounded.sm}` as the default) — this is dense utility UI.
- Let timeline and preview panels dominate screen space; chrome stays minimal.

### Don't
- Don't introduce warm colors (coral, amber, cream) as accents — the system is monochrome-dark + blue, full stop.
- Don't use pure black (#000000) or pure white (#FFFFFF) for large surfaces — both are slightly tinted to avoid harsh contrast during long sessions.
- Don't apply large border radii (16px+) to timeline or track elements — reserve bigger radii for modals/onboarding only.
- Don't use more than one blue step inside the same small component (e.g. don't outline a button in Blue 300 if its fill is Blue 600) — pick one role per element.
- Don't add a serif display face; this system has no editorial typography layer.

## Responsive Behavior

### Breakpoints
This is a desktop-native creative app (macOS-style), so "responsive" means panel resizing/collapsing rather than mobile breakpoints:

| Context | Width | Key Changes |
|---|---|---|
| Compact window | < 1100px | Inspector panel auto-collapses to icon rail; media library shrinks to single-column thumbnails |
| Standard window | 1100–1600px | Full three-pane layout, default panel widths |
| Wide window | > 1600px | Extra width goes to the preview viewer and timeline, not panel widths (panels have a max-width cap) |

### Touch / Click Targets
- `{component.button-icon}` minimum 28×28px (mouse-precision app, smaller than typical touch targets is acceptable).
- `{component.timeline-clip}` minimum clickable height 24px even when track height is compacted.
- Trim handles at clip edges get an 8px-wide invisible hit-area beyond the visible 2px handle.

### Collapsing Strategy
- Left (media/chat) and right (inspector) panels can each collapse to a thin icon rail; the user re-expands by clicking the rail icon.
- The timeline never collapses — it always gets priority height; panels yield space first.
- The AI chat panel can dock, float, or fully hide depending on whether an agent session is active.

## Iteration Guide

1. Focus on ONE component at a time, referencing its key (`{component.timeline-clip}`, `{component.generate-panel}`).
2. Variants (`-active`, `-disabled`, `-focused`, `-selected`) live as separate entries under `components:`.
3. Use `{token.refs}` everywhere — never inline hex in implementation.
4. The blue ramp (`primary` → `accent-secondary` → `accent-tertiary` → `accent-soft` → `accent-pale`) is the only color family besides the dark neutrals and the three semantic colors. Resist adding a sixth.
5. Dark canvas + blue ramp is the trinity. No second accent family (no purple, no green-as-brand, no warm tones).
6. When in doubt about emphasis: deepen the blue step before increasing size or adding a shadow.

## Known Gaps

- Exact timeline zoom/snap behavior and keyboard-shortcut overlays are not specified here — would need an interaction-design pass.
- Multi-monitor / external-preview-window styling is out of scope.
- Onboarding/empty-state illustration style (what fills the canvas before a project is opened) is not designed — currently assumed to reuse the blue ramp at low opacity over the dark canvas.
- Color-managed preview (Rec.709 vs P3 viewer chrome) is a rendering concern, not a UI-token concern, and isn't covered.