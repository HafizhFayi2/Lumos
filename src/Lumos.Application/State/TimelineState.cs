using System;
using System.Collections.Immutable;
using Lumos.Domain;

namespace Lumos.Application.State;

/// Snapshot of the current timeline data. The Timeline object itself is mutable
/// (for command compatibility), but TimelineState wraps it with a monotonically
/// increasing version counter so subscribers can cheaply detect changes.
public sealed class TimelineState
{
    public Timeline Timeline { get; }
    public long Version { get; }

    public int Fps        => Timeline.Fps;
    public int Width      => Timeline.Width;
    public int Height     => Timeline.Height;
    public int TotalFrames => Timeline.TotalFrames;
    public IReadOnlyList<Track> Tracks => Timeline.Tracks;

    public TimelineState(Timeline timeline, long version)
    {
        Timeline = timeline;
        Version  = version;
    }

    public static TimelineState Initial() =>
        new(new Timeline(), 0);

    public TimelineState WithVersion(long version) =>
        new(Timeline, version);
}
