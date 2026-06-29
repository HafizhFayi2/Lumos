using System;
using System.Collections.Immutable;
using Lumos.Domain;

namespace Lumos.Application.State;

/// Composite editor state — single source of truth for the entire editor.
/// Contains all sub-states as immutable slices (except Timeline which is
/// version-tracked via TimelineState).
public sealed record EditorState
{
    public TimelineState Timeline { get; init; } = TimelineState.Initial();
    public SelectionState Selection { get; init; } = new();
    public ViewportState Viewport { get; init; } = new();
    public PlaybackState Playback { get; init; } = new();
    public HistoryState History { get; init; } = HistoryState.Initial(new Timeline());
    public ToolMode ToolMode { get; init; } = ToolMode.Pointer;
    public DragState DragState { get; init; } = new DragState.Idle();
    public PreviewTab ActivePreviewTab { get; init; } = PreviewTab.Timeline;
    public System.Collections.Immutable.ImmutableList<PreviewTab> PreviewTabs { get; init; } = System.Collections.Immutable.ImmutableList.Create<PreviewTab>(PreviewTab.Timeline);
    public PreviewQuality PreviewQuality { get; init; } = PreviewQuality.Half;

    /// Project metadata
    public Guid ProjectId { get; init; } = Guid.NewGuid();
    public string ProjectName { get; init; } = "Untitled Project";
    public bool IsDirty { get; init; }

    // ── Convenience accessors ───────────────────────────────────────────────

    public int PlayheadFrame   => Playback.PlayheadFrame;
    public int Fps             => Timeline.Fps;
    public int TotalFrames     => Timeline.TotalFrames;
    public bool IsPlaying      => Playback.IsPlaying;
    public bool CanUndo        => History.CanUndo;
    public bool CanRedo        => History.CanRedo;
}
