using System;
using System.Collections.Generic;

namespace Palmier.Domain;

public abstract record DragState
{
    public record Idle : DragState;
    public record ScrubPlayhead : DragState;
    public record MoveClip(MoveClipDrag Drag) : DragState;
    public record TrimLeft(TrimDrag Drag) : DragState;
    public record TrimRight(TrimDrag Drag) : DragState;
    public record AudioVolumeKf(AudioVolumeKfDrag Drag) : DragState;
    public record FadeKnee(FadeKneeDrag Drag) : DragState;
    public record Marquee(MarqueeDrag Drag) : DragState;
    public record TimelineRange(TimelineRangeDrag Drag) : DragState;
}

public class AudioVolumeKfDrag
{
    public string ClipId { get; }
    public int TrackIndex { get; }
    public int OriginalFrame { get; }
    public double OriginalDb { get; }
    public int GrabFrame { get; }
    public int CurrentFrame { get; set; }
    public double CurrentDb { get; set; }

    public AudioVolumeKfDrag(string clipId, int trackIndex, int originalFrame, double originalDb, int grabFrame)
    {
        ClipId = clipId;
        TrackIndex = trackIndex;
        OriginalFrame = originalFrame;
        OriginalDb = originalDb;
        GrabFrame = grabFrame;
        CurrentFrame = originalFrame;
        CurrentDb = originalDb;
    }
}

public class FadeKneeDrag
{
    public string ClipId { get; }
    public int TrackIndex { get; }
    public FadeEdge Edge { get; }
    public int OriginalFrames { get; }
    public int GrabFrame { get; }
    public int CurrentFrames { get; set; }

    public FadeKneeDrag(string clipId, int trackIndex, FadeEdge edge, int originalFrames, int grabFrame)
    {
        ClipId = clipId;
        TrackIndex = trackIndex;
        Edge = edge;
        OriginalFrames = originalFrames;
        GrabFrame = grabFrame;
        CurrentFrames = originalFrames;
    }
}

public class MoveClipDrag
{
    public Participant Lead { get; }
    public List<Participant> Companions { get; set; } = new();
    public int GrabOffsetFrames { get; }
    public int DeltaFrames { get; set; } = 0;
    public TrackDropTarget DropTarget { get; set; }
    public bool IsDuplicate { get; }

    public MoveClipDrag(Participant lead, int grabOffsetFrames, TrackDropTarget dropTarget, bool isDuplicate)
    {
        Lead = lead;
        GrabOffsetFrames = grabOffsetFrames;
        DropTarget = dropTarget;
        IsDuplicate = isDuplicate;
    }

    public List<Participant> All
    {
        get
        {
            var list = new List<Participant> { Lead };
            list.AddRange(Companions);
            return list;
        }
    }

    public bool IsLead(Participant p) => p.ClipId == Lead.ClipId;

    public int TrackDelta => DropTarget.Type == TrackDropTargetType.ExistingTrack
        ? DropTarget.Index - Lead.OriginalTrack
        : 0;

    public int? DropTargetTrackIndex => DropTarget.Type == TrackDropTargetType.ExistingTrack
        ? DropTarget.Index
        : null;
}

public record struct Participant(string ClipId, string OriginalTrackId, int OriginalTrack, int OriginalFrame);

public class TrimDrag
{
    public string ClipId { get; }
    public int TrackIndex { get; }
    public int OriginalTrimStart { get; }
    public int OriginalTrimEnd { get; }
    public int OriginalStartFrame { get; }
    public int OriginalDuration { get; }
    public bool HasNoSourceMedia { get; }
    public bool PropagateToLinked { get; }
    public int DeltaFrames { get; set; } = 0;

    public TrimDrag(string clipId, int trackIndex, int originalTrimStart, int originalTrimEnd, int originalStartFrame, int originalDuration, bool hasNoSourceMedia, bool propagateToLinked)
    {
        ClipId = clipId;
        TrackIndex = trackIndex;
        OriginalTrimStart = originalTrimStart;
        OriginalTrimEnd = originalTrimEnd;
        OriginalStartFrame = originalStartFrame;
        OriginalDuration = originalDuration;
        HasNoSourceMedia = hasNoSourceMedia;
        PropagateToLinked = propagateToLinked;
    }
}

public class MarqueeDrag
{
    public DomainPoint Origin { get; }
    public DomainRect Current { get; set; } = default;
    public HashSet<string> BaseSelection { get; set; } = new();

    public MarqueeDrag(DomainPoint origin)
    {
        Origin = origin;
    }
}

public class TimelineRangeDrag
{
    public int AnchorFrame { get; }

    public TimelineRangeDrag(int anchorFrame)
    {
        AnchorFrame = anchorFrame;
    }
}
