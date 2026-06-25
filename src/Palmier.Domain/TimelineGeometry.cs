using System;
using System.Collections.Generic;

namespace Palmier.Domain;

public enum TrackDropTargetType
{
    ExistingTrack,
    NewTrackAt
}

public readonly record struct TrackDropTarget(TrackDropTargetType Type, int Index);

public readonly record struct DomainPoint(double X, double Y);

public readonly record struct DomainRect(double X, double Y, double Width, double Height)
{
    public double MinX => X;
    public double MaxX => X + Width;
    public double MinY => Y;
    public double MaxY => Y + Height;

    public bool Contains(DomainPoint point) =>
        point.X >= X && point.X <= X + Width &&
        point.Y >= Y && point.Y <= Y + Height;
}

/// Pure layout math for the timeline.
public class TimelineGeometry
{
    public double PixelsPerFrame { get; }
    public double HeaderWidth { get; }
    public double RulerHeight { get; }
    public int TrackCount { get; }
    public List<double> TrackHeights { get; }
    public DomainRect Bounds { get; }

    private readonly List<double> _cumulativeY;

    public TimelineGeometry(double pixelsPerFrame, double headerWidth, List<double> trackHeights, DomainRect bounds = default)
    {
        PixelsPerFrame = pixelsPerFrame;
        HeaderWidth = headerWidth;
        RulerHeight = Layout.RulerHeight;
        TrackCount = trackHeights.Count;
        TrackHeights = trackHeights;
        Bounds = bounds;

        _cumulativeY = new List<double>(trackHeights.Count);
        double y = RulerHeight + Layout.DropZoneHeight;
        foreach (double h in trackHeights)
        {
            _cumulativeY.Add(y);
            y += h;
        }
    }

    public double GetTrackHeight(int index)
    {
        return index >= 0 && index < TrackHeights.Count ? TrackHeights[index] : Layout.TrackHeight;
    }

    public double GetTrackY(int index)
    {
        return index >= 0 && index < _cumulativeY.Count ? _cumulativeY[index] : RulerHeight;
    }

    public DomainRect GetClipRect(Clip clip, int trackIndex)
    {
        return GetClipRect(clip, GetTrackY(trackIndex), GetTrackHeight(trackIndex));
    }

    public DomainRect GetClipRect(Clip clip, double y, double h)
    {
        return new DomainRect(
            HeaderWidth + clip.StartFrame * PixelsPerFrame,
            y + 2,
            clip.DurationFrames * PixelsPerFrame,
            h - 4
        );
    }

    public int GetFrameAt(double x)
    {
        return Math.Max(0, (int)((x - HeaderWidth) / PixelsPerFrame));
    }

    public int GetTrackAt(double y)
    {
        for (int i = 0; i < _cumulativeY.Count; i++)
        {
            if (y < _cumulativeY[i] + TrackHeights[i]) return i;
        }
        return Math.Max(0, TrackCount - 1);
    }

    public TrackDropTarget GetDropTargetAt(double y)
    {
        if (TrackCount == 0) return new TrackDropTarget(TrackDropTargetType.NewTrackAt, 0);

        if (y < _cumulativeY[0])
        {
            return new TrackDropTarget(TrackDropTargetType.NewTrackAt, 0);
        }

        double threshold = Layout.InsertThreshold;
        for (int i = 0; i < TrackCount - 1; i++)
        {
            double bottomOfTrack = _cumulativeY[i] + TrackHeights[i];
            double topOfNext = _cumulativeY[i + 1];
            if (y >= bottomOfTrack - threshold && y <= topOfNext + threshold)
            {
                return new TrackDropTarget(TrackDropTargetType.NewTrackAt, i + 1);
            }
        }

        double lastTrackBottom = _cumulativeY[TrackCount - 1] + TrackHeights[TrackCount - 1];
        if (y >= lastTrackBottom)
        {
            return new TrackDropTarget(TrackDropTargetType.NewTrackAt, TrackCount);
        }

        for (int i = 0; i < _cumulativeY.Count; i++)
        {
            if (y < _cumulativeY[i] + TrackHeights[i]) return new TrackDropTarget(TrackDropTargetType.ExistingTrack, i);
        }
        return new TrackDropTarget(TrackDropTargetType.ExistingTrack, Math.Max(0, TrackCount - 1));
    }

    public double? GetInsertionLineY(TrackDropTarget target)
    {
        if (target.Type == TrackDropTargetType.ExistingTrack) return null;

        int index = target.Index;
        if (TrackCount == 0)
        {
            return RulerHeight + Layout.DropZoneHeight;
        }
        else if (index == 0)
        {
            return _cumulativeY[0];
        }
        else if (index >= TrackCount)
        {
            return _cumulativeY[TrackCount - 1] + TrackHeights[TrackCount - 1];
        }
        else
        {
            return _cumulativeY[index];
        }
    }

    public double? GetGhostY(TrackDropTarget target, double height = Layout.TrackHeight)
    {
        if (target.Type != TrackDropTargetType.NewTrackAt) return null;
        double? lineY = GetInsertionLineY(target);
        if (lineY == null) return null;
        return target.Index < TrackCount ? lineY.Value - height : lineY.Value;
    }

    public double GetXForFrame(int frame)
    {
        return HeaderWidth + frame * PixelsPerFrame;
    }
}
