using System;

namespace Lumos.Application.State;

public enum PlaybackMode
{
    Stopped,
    Playing,
    Paused,
    Scrubbing
}

/// Immutable playback state. Tracks playhead position, playback mode, and loop markers.
public sealed record PlaybackState
{
    public int PlayheadFrame { get; init; }
    public PlaybackMode Mode { get; init; } = PlaybackMode.Stopped;
    public double PlaybackRate { get; init; } = 1.0;
    public bool IsLooping { get; init; }
    public int LoopInFrame { get; init; }
    public int LoopOutFrame { get; init; }

    public bool IsPlaying   => Mode == PlaybackMode.Playing;
    public bool IsScrubbing => Mode == PlaybackMode.Scrubbing;

    public TimeSpan PlayheadTime(int fps) =>
        fps > 0 ? TimeSpan.FromSeconds((double)PlayheadFrame / fps) : TimeSpan.Zero;

    // ── Transition methods ──────────────────────────────────────────────────

    public PlaybackState Play() =>
        this with { Mode = PlaybackMode.Playing };

    public PlaybackState Pause() =>
        this with { Mode = PlaybackMode.Paused };

    public PlaybackState Stop() =>
        this with { Mode = PlaybackMode.Stopped, PlayheadFrame = 0 };

    public PlaybackState StartScrub() =>
        this with { Mode = PlaybackMode.Scrubbing };

    public PlaybackState EndScrub() =>
        this with { Mode = PlaybackMode.Paused };

    public PlaybackState SetPlayhead(int frame, int totalFrames) =>
        this with { PlayheadFrame = Math.Clamp(frame, 0, Math.Max(0, totalFrames)) };

    public PlaybackState StepForward(int totalFrames) =>
        SetPlayhead(PlayheadFrame + 1, totalFrames);

    public PlaybackState StepBackward(int totalFrames) =>
        SetPlayhead(PlayheadFrame - 1, totalFrames);

    public PlaybackState SetRate(double rate) =>
        this with { PlaybackRate = Math.Clamp(rate, 0.1, 16.0) };

    public PlaybackState SetLoop(bool enabled, int inFrame = 0, int outFrame = 0) =>
        this with { IsLooping = enabled, LoopInFrame = inFrame, LoopOutFrame = outFrame };
}
