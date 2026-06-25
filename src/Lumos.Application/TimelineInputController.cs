using Lumos.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lumos.Application;

public interface ITimelineViewContext
{
    double ScrollOffsetX { get; }
    double ScrollOffsetY { get; }
    double ViewportWidth { get; }
    double ViewportHeight { get; }
    void RefreshView();
    void SetSnapIndicatorX(double? x);
    bool AutoScrollHorizontallyForTimelineDrag(DomainPoint point);
}

public class TimelineInputController
{
    private readonly CommandDispatcher _dispatcher;
    private readonly Timeline _timeline;
    private readonly LegacyEditorState _editorState;
    private readonly ITimelineViewContext _viewContext;

    public DragState DragState { get; private set; } = new DragState.Idle();
    
    private double? _snapIndicatorX;
    public double? SnapIndicatorX
    {
        get => _snapIndicatorX;
        private set
        {
            _snapIndicatorX = value;
            _viewContext.SetSnapIndicatorX(value);
        }
    }

    public int? RazorPreviewFrame { get; private set; }

    private SnapState _snapState = new();
    private SnapState _razorSnapState = new();
    private bool _scrubWasPlaying = false;

    public TimelineInputController(
        CommandDispatcher dispatcher,
        Timeline timeline,
        LegacyEditorState editorState,
        ITimelineViewContext viewContext)
    {
        _dispatcher = dispatcher;
        _timeline = timeline;
        _editorState = editorState;
        _viewContext = viewContext;
    }

    public void OnMouseDown(DomainPoint point, bool isShift, bool isOption, bool isCommand, int clickCount, TimelineGeometry geometry)
    {
        double scrollOffsetY = _viewContext.ScrollOffsetY;

        if (clickCount == 2 && point.Y >= scrollOffsetY + geometry.RulerHeight)
        {
            int ti = geometry.GetTrackAt(point.Y);
            var hit = HitTestClip(point, ti, geometry);
            if (hit.HasValue)
            {
                DragState = new DragState.Idle();
                _viewContext.RefreshView();
                return;
            }
        }

        if (point.Y >= scrollOffsetY && point.Y < scrollOffsetY + geometry.RulerHeight)
        {
            int frame = geometry.GetFrameAt(point.X);
            if (isShift)
            {
                BeginTimelineRangeSelection(frame);
            }
            else
            {
                BeginPlayheadScrub(frame);
            }
            return;
        }

        int trackIndex = geometry.GetTrackAt(point.Y);

        if (trackIndex >= 0 && trackIndex < _timeline.Tracks.Count)
        {
            _editorState.ActiveTrackId = Guid.Parse(_timeline.Tracks[trackIndex].Id);
        }

        if (ToolMode == ToolMode.Razor)
        {
            var hit = HitTestClip(point, trackIndex, geometry);
            if (hit.HasValue)
            {
                int clickFrame = RazorPreviewFrame ?? geometry.GetFrameAt(point.X);
                var clip = _timeline.Tracks[hit.Value.TrackIndex].Clips[hit.Value.ClipIndex];
                var cmd = new SplitClipCommand(_timeline, clip.Id, clickFrame);
                _dispatcher.Execute(cmd);
                _viewContext.RefreshView();
            }
            return;
        }

        var clipHit = HitTestClip(point, trackIndex, geometry);
        if (clipHit.HasValue)
        {
            var clip = _timeline.Tracks[clipHit.Value.TrackIndex].Clips[clipHit.Value.ClipIndex];
            var rect = geometry.GetClipRect(clip, clipHit.Value.TrackIndex);

            var selectedIds = new HashSet<string>(_editorState.SelectedClipIds.Select(g => g.ToString()));
            bool linkedOn = !isOption;

            if (isShift)
            {
                if (selectedIds.Contains(clip.Id))
                {
                    if (linkedOn)
                    {
                        var group = ExpandToLinkGroup(clip.Id);
                        selectedIds.ExceptWith(group);
                    }
                    else
                    {
                        selectedIds.Remove(clip.Id);
                    }
                }
                else
                {
                    if (linkedOn)
                    {
                        var group = ExpandToLinkGroup(clip.Id);
                        selectedIds.UnionWith(group);
                    }
                    else
                    {
                        selectedIds.Add(clip.Id);
                    }
                }
            }
            else
            {
                if (!selectedIds.Contains(clip.Id))
                {
                    selectedIds.Clear();
                    if (linkedOn)
                    {
                        var group = ExpandToLinkGroup(clip.Id);
                        selectedIds.UnionWith(group);
                    }
                    else
                    {
                        selectedIds.Add(clip.Id);
                    }
                }
            }

            _editorState.SelectedClipIds = selectedIds.Select(Guid.Parse).ToList();

            double localX = point.X - rect.X;

            if (!isOption && localX <= Trim.HandleWidth)
            {
                DragState = new DragState.TrimLeft(new TrimDrag(
                    clip.Id,
                    clipHit.Value.TrackIndex,
                    clip.TrimStartFrame,
                    clip.TrimEndFrame,
                    clip.StartFrame,
                    clip.DurationFrames,
                    clip.MediaType == ClipType.Image || clip.MediaType == ClipType.Text,
                    linkedOn
                ));
            }
            else if (!isOption && localX >= rect.Width - Trim.HandleWidth)
            {
                DragState = new DragState.TrimRight(new TrimDrag(
                    clip.Id,
                    clipHit.Value.TrackIndex,
                    clip.TrimStartFrame,
                    clip.TrimEndFrame,
                    clip.StartFrame,
                    clip.DurationFrames,
                    clip.MediaType == ClipType.Image || clip.MediaType == ClipType.Text,
                    linkedOn
                ));
            }
            else
            {
                int grabFrame = geometry.GetFrameAt(point.X);
                var companions = new List<Participant>();
                for (int ti = 0; ti < _timeline.Tracks.Count; ti++)
                {
                    var track = _timeline.Tracks[ti];
                    foreach (var c in track.Clips)
                    {
                        if (c.Id != clip.Id && selectedIds.Contains(c.Id))
                        {
                            companions.Add(new Participant(c.Id, track.Id, ti, c.StartFrame));
                        }
                    }
                }

                DragState = new DragState.MoveClip(new MoveClipDrag(
                    new Participant(clip.Id, _timeline.Tracks[clipHit.Value.TrackIndex].Id, clipHit.Value.TrackIndex, clip.StartFrame),
                    grabFrame - clip.StartFrame,
                    new TrackDropTarget(TrackDropTargetType.ExistingTrack, clipHit.Value.TrackIndex),
                    isOption
                ) { Companions = companions });
            }
        }
        else
        {
            if (!isShift)
            {
                _editorState.SelectedClipIds.Clear();
            }
            DragState = new DragState.Marquee(new MarqueeDrag(point)
            {
                BaseSelection = new HashSet<string>(_editorState.SelectedClipIds.Select(g => g.ToString()))
            });
        }

        _snapState = new SnapState();
        _viewContext.RefreshView();
    }

    public void OnMouseDrag(DomainPoint point, bool isOption, TimelineGeometry geometry)
    {
        if (DragState is DragState.ScrubPlayhead)
        {
            int frame = geometry.GetFrameAt(point.X);
            ScrubToFrame(frame);
            _viewContext.RefreshView();
            return;
        }

        int framePos = geometry.GetFrameAt(point.X);

        switch (DragState)
        {
            case DragState.MoveClip moveDrag:
                var drag = moveDrag.Drag;
                int candidateFrame = framePos - drag.GrabOffsetFrames;
                var allDraggedIds = new HashSet<string>(drag.All.Select(p => p.ClipId));
                var moveTargets = SnapEngine.CollectTargets(_timeline.Tracks, GetPlayheadFrame(), allDraggedIds, includePlayhead: true);

                var probeOffsets = new List<int>();
                foreach (var p in drag.All)
                {
                    var c = FindClipById(p.ClipId);
                    if (c != null)
                    {
                        int baseOffset = p.OriginalFrame - drag.Lead.OriginalFrame;
                        probeOffsets.Add(baseOffset);
                        probeOffsets.Add(baseOffset + c.DurationFrames);
                    }
                }

                var moveSnap = SnapEngine.FindSnap(candidateFrame, probeOffsets, moveTargets, ref _snapState, Snap.ThresholdPixels, geometry.PixelsPerFrame);
                if (moveSnap.HasValue)
                {
                    SnapIndicatorX = moveSnap.Value.X;
                    drag.DeltaFrames = (moveSnap.Value.Frame - moveSnap.Value.ProbeOffset) - drag.Lead.OriginalFrame;
                }
                else
                {
                    SnapIndicatorX = null;
                    drag.DeltaFrames = candidateFrame - drag.Lead.OriginalFrame;
                }

                int minOrigFrame = drag.All.Min(p => p.OriginalFrame);
                drag.DeltaFrames = Math.Max(-minOrigFrame, drag.DeltaFrames);

                var cursorTarget = geometry.GetDropTargetAt(point.Y);
                if (cursorTarget.Type == TrackDropTargetType.ExistingTrack)
                {
                    int leadTrack = drag.Lead.OriginalTrack;
                    int clamped = ClampedTrackDelta(drag, cursorTarget.Index - leadTrack);
                    drag.DropTarget = new TrackDropTarget(TrackDropTargetType.ExistingTrack, leadTrack + clamped);
                }
                else
                {
                    drag.DropTarget = cursorTarget;
                }
                break;

            case DragState.TrimLeft trimLeftDrag:
                var trimL = trimLeftDrag.Drag;
                var trimLTargets = SnapEngine.CollectTargets(_timeline.Tracks, GetPlayheadFrame(), new HashSet<string> { trimL.ClipId }, includePlayhead: true);
                int snappedStart = framePos;
                var trimLSnap = SnapEngine.FindSnap(framePos, new List<int> { 0 }, trimLTargets, ref _snapState, Snap.ThresholdPixels, geometry.PixelsPerFrame);
                if (trimLSnap.HasValue)
                {
                    SnapIndicatorX = trimLSnap.Value.X;
                    snappedStart = trimLSnap.Value.Frame;
                }
                else
                {
                    SnapIndicatorX = null;
                }
                int deltaL = snappedStart - trimL.OriginalStartFrame;
                int maxDeltaL = trimL.OriginalDuration - 1;
                int minDeltaL = trimL.HasNoSourceMedia ? -trimL.OriginalStartFrame : -trimL.OriginalTrimStart;
                trimL.DeltaFrames = Math.Max(minDeltaL, Math.Min(maxDeltaL, deltaL));
                break;

            case DragState.TrimRight trimRightDrag:
                var trimR = trimRightDrag.Drag;
                int originalEndFrame = trimR.OriginalStartFrame + trimR.OriginalDuration;
                int candidateEnd = Math.Max(trimR.OriginalStartFrame + 1, framePos);
                var trimRTargets = SnapEngine.CollectTargets(_timeline.Tracks, GetPlayheadFrame(), new HashSet<string> { trimR.ClipId }, includePlayhead: true);
                int snappedEnd = candidateEnd;
                var trimRSnap = SnapEngine.FindSnap(candidateEnd, new List<int> { 0 }, trimRTargets, ref _snapState, Snap.ThresholdPixels, geometry.PixelsPerFrame);
                if (trimRSnap.HasValue)
                {
                    SnapIndicatorX = trimRSnap.Value.X;
                    snappedEnd = trimRSnap.Value.Frame;
                }
                else
                {
                    SnapIndicatorX = null;
                }
                trimR.DeltaFrames = snappedEnd - originalEndFrame;
                int minDeltaR = -(trimR.OriginalDuration - 1);
                if (trimR.HasNoSourceMedia)
                {
                    trimR.DeltaFrames = Math.Max(minDeltaR, trimR.DeltaFrames);
                }
                else
                {
                    int maxDeltaR = trimR.OriginalTrimEnd;
                    trimR.DeltaFrames = Math.Max(minDeltaR, Math.Min(maxDeltaR, trimR.DeltaFrames));
                }
                break;

            case DragState.Marquee marqueeDrag:
                var marq = marqueeDrag.Drag;
                marq.Current = new DomainRect(
                    Math.Min(marq.Origin.X, point.X),
                    Math.Min(marq.Origin.Y, point.Y),
                    Math.Abs(point.X - marq.Origin.X),
                    Math.Abs(point.Y - marq.Origin.Y)
                );

                var selected = new HashSet<string>(marq.BaseSelection);
                for (int ti = 0; ti < _timeline.Tracks.Count; ti++)
                {
                    var track = _timeline.Tracks[ti];
                    foreach (var clip in track.Clips)
                    {
                        var clipRect = geometry.GetClipRect(clip, ti);
                        if (Intersects(clipRect, marq.Current))
                        {
                            selected.Add(clip.Id);
                        }
                    }
                }

                if (!isOption)
                {
                    var expanded = new List<string>();
                    foreach (var id in selected)
                    {
                        expanded.AddRange(ExpandToLinkGroup(id));
                    }
                    selected.UnionWith(expanded);
                }

                _editorState.SelectedClipIds = selected.Select(Guid.Parse).ToList();
                break;
        }

        _viewContext.RefreshView();
    }

    public void OnMouseUp()
    {
        switch (DragState)
        {
            case DragState.MoveClip moveDrag:
                var drag = moveDrag.Drag;
                if (drag.DropTarget.Type == TrackDropTargetType.ExistingTrack &&
                    drag.DropTarget.Index == drag.Lead.OriginalTrack && drag.DeltaFrames == 0)
                {
                    break;
                }

                var resolved = ResolvedMoveParticipants(drag);
                if (resolved.Count == 0) break;

                int minOrigFrame = resolved.Min(r => r.frame);
                int frameDelta = Math.Max(-minOrigFrame, drag.DeltaFrames);
                var pinned = PinnedCompanionIds(drag);

                if (drag.DropTarget.Type == TrackDropTargetType.ExistingTrack)
                {
                    int delta = (drag.DropTargetTrackIndex ?? drag.Lead.OriginalTrack) - drag.Lead.OriginalTrack;
                    foreach (var item in resolved)
                    {
                        var p = item.participant;
                        int toTrackIndex = pinned.Contains(p.ClipId) ? item.trackIndex : item.trackIndex + delta;
                        if (toTrackIndex >= 0 && toTrackIndex < _timeline.Tracks.Count)
                        {
                            var cmd = new MoveClipCommand(_timeline, p.ClipId, item.frame + frameDelta, _timeline.Tracks[toTrackIndex].Id);
                            _dispatcher.Execute(cmd);
                        }
                    }
                }
                break;

            case DragState.TrimLeft trimLeftDrag:
                var trimL = trimLeftDrag.Drag;
                if (trimL.DeltaFrames != 0)
                {
                    var cmd = new TrimClipCommand(_timeline, trimL.ClipId, trimL.OriginalTrimStart + trimL.DeltaFrames, trimL.OriginalTrimEnd);
                    _dispatcher.Execute(cmd);
                }
                break;

            case DragState.TrimRight trimRightDrag:
                var trimR = trimRightDrag.Drag;
                if (trimR.DeltaFrames != 0)
                {
                    var cmd = new TrimClipCommand(_timeline, trimR.ClipId, trimR.OriginalTrimStart, trimR.OriginalTrimEnd - trimR.DeltaFrames);
                    _dispatcher.Execute(cmd);
                }
                break;
        }

        DragState = new DragState.Idle();
        SnapIndicatorX = null;
        _viewContext.RefreshView();
    }

    public void OnMouseMove(DomainPoint point, bool isCommand, TimelineGeometry geometry)
    {
        double scrollOffsetY = _viewContext.ScrollOffsetY;

        if (point.Y >= scrollOffsetY && point.Y < scrollOffsetY + geometry.RulerHeight)
        {
            RazorPreviewFrame = null;
            return;
        }

        if (ToolMode == ToolMode.Razor && point.Y >= scrollOffsetY + geometry.RulerHeight)
        {
            int candidate = geometry.GetFrameAt(point.X);
            var targets = SnapEngine.CollectTargets(_timeline.Tracks, GetPlayheadFrame(), includePlayhead: true);
            var snapResult = SnapEngine.FindSnap(candidate, new List<int> { 0 }, targets, ref _razorSnapState, Snap.ThresholdPixels, geometry.PixelsPerFrame);
            if (snapResult.HasValue)
            {
                RazorPreviewFrame = snapResult.Value.Frame;
            }
            else
            {
                RazorPreviewFrame = candidate;
            }
            _viewContext.RefreshView();
            return;
        }

        RazorPreviewFrame = null;
    }

    public ToolMode ToolMode { get; set; } = ToolMode.Pointer;

    private int GetPlayheadFrame()
    {
        return (int)Math.Round(_editorState.PlayheadPosition.TotalSeconds * _timeline.Fps);
    }

    private void ScrubToFrame(int frame)
    {
        _editorState.PlayheadPosition = TimeSpan.FromSeconds((double)frame / _timeline.Fps);
    }

    private Clip? FindClipById(string id)
    {
        return _timeline.Tracks.SelectMany(t => t.Clips).FirstOrDefault(c => c.Id == id);
    }

    private List<string> ExpandToLinkGroup(string id)
    {
        var list = new List<string> { id };
        var c = FindClipById(id);
        if (c != null && !string.IsNullOrEmpty(c.LinkGroupId))
        {
            var group = _timeline.Tracks.SelectMany(t => t.Clips)
                .Where(x => x.Id != id && x.LinkGroupId == c.LinkGroupId)
                .Select(x => x.Id);
            list.AddRange(group);
        }
        return list;
    }

    private List<(Participant participant, int trackIndex, int frame)> ResolvedMoveParticipants(MoveClipDrag drag)
    {
        var resolved = new List<(Participant participant, int trackIndex, int frame)>();
        foreach (var p in drag.All)
        {
            for (int ti = 0; ti < _timeline.Tracks.Count; ti++)
            {
                var track = _timeline.Tracks[ti];
                int cIdx = track.Clips.FindIndex(c => c.Id == p.ClipId);
                if (cIdx != -1)
                {
                    resolved.Add((p, ti, track.Clips[cIdx].StartFrame));
                    break;
                }
            }
        }
        return resolved;
    }

    private HashSet<string> PinnedCompanionIds(MoveClipDrag drag)
    {
        var pinned = new HashSet<string>();
        var leadClip = FindClipById(drag.Lead.ClipId);
        if (leadClip == null) return pinned;

        foreach (var track in _timeline.Tracks)
        {
            foreach (var c in track.Clips)
            {
                if (c.Id != drag.Lead.ClipId)
                {
                    if (!string.IsNullOrEmpty(leadClip.LinkGroupId) && c.LinkGroupId == leadClip.LinkGroupId)
                    {
                        pinned.Add(c.Id);
                    }
                    else if (leadClip.MediaType != c.MediaType)
                    {
                        pinned.Add(c.Id);
                    }
                }
            }
        }
        return pinned;
    }

    private int ClampedTrackDelta(MoveClipDrag drag, int proposed)
    {
        var pinned = PinnedCompanionIds(drag);
        var movers = drag.All.Where(p => !pinned.Contains(p.ClipId)).ToList();
        int step = proposed >= 0 ? -1 : 1;
        int d = proposed;

        while (d != 0)
        {
            bool ok = true;
            foreach (var p in movers)
            {
                int dest = p.OriginalTrack + d;
                if (dest < 0 || dest >= _timeline.Tracks.Count)
                {
                    ok = false;
                    break;
                }
                var c = FindClipById(p.ClipId);
                if (c == null)
                {
                    ok = false;
                    break;
                }
                var destType = _timeline.Tracks[dest].Type;
                if (destType != c.MediaType)
                {
                    if (destType == ClipType.Audio || c.MediaType == ClipType.Audio)
                    {
                        ok = false;
                        break;
                    }
                }
            }

            if (ok) return d;
            d += step;
        }

        return 0;
    }

    private ClipLocation? HitTestClip(DomainPoint point, int trackIndex, TimelineGeometry geometry)
    {
        if (trackIndex < 0 || trackIndex >= _timeline.Tracks.Count) return null;
        var track = _timeline.Tracks[trackIndex];
        for (int ci = 0; ci < track.Clips.Count; ci++)
        {
            var clip = track.Clips[ci];
            var clipRect = geometry.GetClipRect(clip, trackIndex);
            if (clipRect.Contains(point))
            {
                return new ClipLocation(trackIndex, ci);
            }
        }
        return null;
    }

    private void BeginTimelineRangeSelection(int frame)
    {
        // Selection logic can be added later as needed
    }

    private void BeginPlayheadScrub(int frame)
    {
        DragState = new DragState.ScrubPlayhead();
        ScrubToFrame(frame);
    }

    private bool Intersects(DomainRect r1, DomainRect r2)
    {
        return !(r2.MinX > r1.MaxX ||
                 r2.MaxX < r1.MinX ||
                 r2.MinY > r1.MaxY ||
                 r2.MaxY < r1.MinY);
    }
}
