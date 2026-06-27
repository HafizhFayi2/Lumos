using System;
using System.Threading;
using Lumos.Domain;

namespace Lumos.Application.State;

/// Centralized state container. All editor mutations flow through here.
/// Thread-safe: mutations are serialized via a lock.
/// Observable: subscribers receive StateChanged events with the changed field mask.
public sealed class EditorStore
{
    private readonly object _lock = new();
    private EditorState _state;
    private long _timelineVersion;

    public EditorState State
    {
        get { lock (_lock) return _state; }
    }

    public event EventHandler<StateChangedEventArgs>? StateChanged;

    public EditorStore()
    {
        _state = new EditorState();
    }

    public EditorStore(EditorState initial)
    {
        _state = initial;
    }

    // ── Generic mutation ────────────────────────────────────────────────────

    /// Apply an arbitrary state transition. The mutator receives the current state
    /// and returns the new state plus a field mask indicating what changed.
    public void Dispatch(Func<EditorState, (EditorState Next, StateField Changed)> mutator)
    {
        StateField changed;
        lock (_lock)
        {
            var (next, mask) = mutator(_state);
            if (mask == StateField.None) return;
            _state  = next;
            changed = mask;
        }
        RaiseChanged(changed);
    }

    // ── Timeline mutations (with history) ───────────────────────────────────

    /// Execute a timeline mutation with undo tracking.
    /// The mutator receives the mutable Timeline and modifies it in place.
    public void MutateTimeline(string label, Action<Timeline> mutator)
    {
        lock (_lock)
        {
            var timeline = _state.Timeline.Timeline;
            var history  = _state.History.Push(label, timeline);
            mutator(timeline);
            var version  = Interlocked.Increment(ref _timelineVersion);
            _state = _state with
            {
                Timeline = new TimelineState(timeline, version),
                History  = history,
                IsDirty  = true
            };
        }
        RaiseChanged(StateField.Timeline | StateField.History);
    }

    /// Execute a timeline mutation without creating a history entry.
    /// Used for transient changes during drags.
    public void MutateTimelineSilent(Action<Timeline> mutator)
    {
        lock (_lock)
        {
            mutator(_state.Timeline.Timeline);
            var version = Interlocked.Increment(ref _timelineVersion);
            _state = _state with
            {
                Timeline = new TimelineState(_state.Timeline.Timeline, version),
                IsDirty  = true
            };
        }
        RaiseChanged(StateField.Timeline);
    }

    // ── Selection ───────────────────────────────────────────────────────────

    public void UpdateSelection(Func<SelectionState, SelectionState> updater)
    {
        lock (_lock)
        {
            var next = updater(_state.Selection);
            if (ReferenceEquals(next, _state.Selection)) return;
            _state = _state with { Selection = next };
        }
        RaiseChanged(StateField.Selection);
    }

    // ── Viewport ────────────────────────────────────────────────────────────

    public void UpdateViewport(Func<ViewportState, ViewportState> updater)
    {
        lock (_lock)
        {
            var next = updater(_state.Viewport);
            if (next == _state.Viewport) return;
            _state = _state with { Viewport = next };
        }
        RaiseChanged(StateField.Viewport);
    }

    // ── Playback ────────────────────────────────────────────────────────────

    public void UpdatePlayback(Func<PlaybackState, PlaybackState> updater)
    {
        lock (_lock)
        {
            var next = updater(_state.Playback);
            if (next == _state.Playback) return;
            _state = _state with { Playback = next };
        }
        RaiseChanged(StateField.Playback);
    }

    // ── Tool mode ───────────────────────────────────────────────────────────

    public void SetToolMode(ToolMode mode)
    {
        lock (_lock)
        {
            if (_state.ToolMode == mode) return;
            _state = _state with { ToolMode = mode };
        }
        RaiseChanged(StateField.ToolMode);
    }

    // ── Drag state ──────────────────────────────────────────────────────────

    public void SetDragState(DragState drag)
    {
        lock (_lock)
        {
            _state = _state with { DragState = drag };
        }
        RaiseChanged(StateField.DragState);
    }

    // ── Track property toggles ──────────────────────────────────────────────

    public void MuteTimeline(string label, string trackId)
    {
        MutateTimeline(label, t =>
        {
            var track = t.Tracks.FirstOrDefault(tr => tr.Id == trackId);
            if (track != null) track.IsMuted = !track.IsMuted;
        });
    }

    public void HideTimeline(string label, string trackId)
    {
        MutateTimeline(label, t =>
        {
            var track = t.Tracks.FirstOrDefault(tr => tr.Id == trackId);
            if (track != null) track.IsHidden = !track.IsHidden;
        });
    }

    public void LockTimeline(string label, string trackId)
    {
        MutateTimeline(label, t =>
        {
            var track = t.Tracks.FirstOrDefault(tr => tr.Id == trackId);
            if (track != null) track.IsSyncLocked = !track.IsSyncLocked;
        });
    }

    // ── Undo / Redo ─────────────────────────────────────────────────────────

    public void Undo()
    {
        lock (_lock)
        {
            if (!_state.History.CanUndo) return;
            var (history, snapshot) = _state.History.Undo();
            var version = Interlocked.Increment(ref _timelineVersion);
            _state = _state with
            {
                Timeline = new TimelineState(snapshot, version),
                History  = history
            };
        }
        RaiseChanged(StateField.Timeline | StateField.History | StateField.Selection);
    }

    public void Redo()
    {
        lock (_lock)
        {
            if (!_state.History.CanRedo) return;
            var (history, snapshot) = _state.History.Redo();
            var version = Interlocked.Increment(ref _timelineVersion);
            _state = _state with
            {
                Timeline = new TimelineState(snapshot, version),
                History  = history
            };
        }
        RaiseChanged(StateField.Timeline | StateField.History | StateField.Selection);
    }

    // ── Project metadata ────────────────────────────────────────────────────

    public void SetProject(Guid id, string name, Timeline timeline)
    {
        lock (_lock)
        {
            var version = Interlocked.Increment(ref _timelineVersion);
            _state = new EditorState
            {
                ProjectId   = id,
                ProjectName = name,
                Timeline    = new TimelineState(timeline, version),
                History     = HistoryState.Initial(timeline),
                Selection   = new SelectionState(),
                Viewport    = _state.Viewport,
                Playback    = new PlaybackState(),
                ToolMode    = ToolMode.Pointer,
                DragState   = new DragState.Idle(),
                IsDirty     = false
            };
        }
        RaiseChanged(StateField.All);
    }

    // ── Preview Tabs ────────────────────────────────────────────────────────

    public void OpenPreviewTab(PreviewTab tab)
    {
        lock (_lock)
        {
            if (!_state.PreviewTabs.Any(t => t.Id == tab.Id))
            {
                var tabs = _state.PreviewTabs.Add(tab);
                _state = _state with { PreviewTabs = tabs, ActivePreviewTab = tab };
            }
            else
            {
                var existing = _state.PreviewTabs.First(t => t.Id == tab.Id);
                _state = _state with { ActivePreviewTab = existing };
            }
        }
        RaiseChanged(StateField.PreviewTab);
    }

    public void SelectPreviewTab(string tabId)
    {
        lock (_lock)
        {
            var tab = _state.PreviewTabs.FirstOrDefault(t => t.Id == tabId);
            if (tab == null || _state.ActivePreviewTab == tab) return;
            _state = _state with { ActivePreviewTab = tab };
        }
        RaiseChanged(StateField.PreviewTab);
    }

    public void ClosePreviewTab(string tabId)
    {
        lock (_lock)
        {
            var tab = _state.PreviewTabs.FirstOrDefault(t => t.Id == tabId);
            if (tab == null || !tab.IsCloseable) return;

            int index = _state.PreviewTabs.IndexOf(tab);
            var tabs = _state.PreviewTabs.Remove(tab);
            var active = _state.ActivePreviewTab;
            if (active.Id == tabId)
            {
                active = tabs[Math.Max(0, index - 1)];
            }
            _state = _state with { PreviewTabs = tabs, ActivePreviewTab = active };
        }
        RaiseChanged(StateField.PreviewTab);
    }

    public void CloseAllPreviewTabs()
    {
        lock (_lock)
        {
            var tabs = System.Collections.Immutable.ImmutableList.Create<PreviewTab>(PreviewTab.Timeline);
            _state = _state with { PreviewTabs = tabs, ActivePreviewTab = PreviewTab.Timeline };
        }
        RaiseChanged(StateField.PreviewTab);
    }

    public void MarkClean()
    {
        lock (_lock) { _state = _state with { IsDirty = false }; }
    }

    // ── Internal ────────────────────────────────────────────────────────────

    private void RaiseChanged(StateField fields) =>
        StateChanged?.Invoke(this, new StateChangedEventArgs(fields));
}
