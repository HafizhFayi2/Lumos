using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Palmier.Application;
using Palmier.Domain;

namespace Palmier.Media;

/// Primary playback engine. Drives the playhead clock via a background loop,
/// raises PositionChanged on every frame boundary, and delegates frame
/// resolution to CompositionBuilder.
public sealed class PlaybackEngine : IPlaybackEngine
{
    private readonly CompositionBuilder _composer = new();
    private readonly PlaybackClock _clock = new();

    private Timeline? _timeline;
    private CancellationTokenSource? _cts;
    private bool _isPlaying;
    private TimeSpan _currentPosition;

    public bool   IsPlaying        => _isPlaying;
    public TimeSpan CurrentPosition => _currentPosition;
    public TimeSpan Duration
    {
        get
        {
            if (_timeline is null) return TimeSpan.Zero;
            int fps = _timeline.Fps > 0 ? _timeline.Fps : 30;
            return TimeSpan.FromSeconds((double)_timeline.TotalFrames / fps);
        }
    }

    public event EventHandler<TimeSpan>? PositionChanged;

    public void LoadTimeline(Timeline timeline)
    {
        Stop();
        _timeline = timeline;
        _composer.Load(timeline);
        _currentPosition = TimeSpan.Zero;
    }

    public void Play()
    {
        if (_isPlaying || _timeline is null) return;
        _isPlaying = true;

        int fps = _timeline.Fps > 0 ? _timeline.Fps : 30;
        int startFrame = PositionToFrame(_currentPosition, fps);
        _clock.Start(startFrame, fps);

        _cts = new CancellationTokenSource();
        _ = RunPlaybackLoopAsync(_cts.Token, fps);
    }

    public void Pause()
    {
        if (!_isPlaying) return;
        _isPlaying = false;
        _clock.Stop();
        _cts?.Cancel();
    }

    public void Stop()
    {
        _isPlaying = false;
        _clock.Stop();
        _cts?.Cancel();
        _cts = null;
        _currentPosition = TimeSpan.Zero;
        RaisePositionChanged(_currentPosition);
    }

    public void Seek(TimeSpan position)
    {
        if (_timeline is null) return;
        _currentPosition = Clamp(position, TimeSpan.Zero, Duration);

        if (_isPlaying)
        {
            int fps = _timeline.Fps > 0 ? _timeline.Fps : 30;
            _clock.Start(PositionToFrame(_currentPosition, fps), fps);
        }

        RaisePositionChanged(_currentPosition);
    }

    public void StepForward()
    {
        if (_timeline is null) return;
        int fps = _timeline.Fps > 0 ? _timeline.Fps : 30;
        Seek(_currentPosition + TimeSpan.FromSeconds(1.0 / fps));
    }

    public void StepBackward()
    {
        if (_timeline is null) return;
        int fps = _timeline.Fps > 0 ? _timeline.Fps : 30;
        Seek(_currentPosition - TimeSpan.FromSeconds(1.0 / fps));
    }

    private async Task RunPlaybackLoopAsync(CancellationToken ct, int fps)
    {
        int frameDurationMs = fps > 0 ? 1000 / fps : 33;

        while (!ct.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();

            int frame = _clock.GetCurrentFrame();
            _currentPosition = FrameToPosition(frame, fps);

            if (_timeline is not null && frame >= _timeline.TotalFrames)
            {
                // Reached end — stop and park at last frame
                _currentPosition = Duration;
                RaisePositionChanged(_currentPosition);
                _isPlaying = false;
                _clock.Stop();
                return;
            }

            RaisePositionChanged(_currentPosition);

            long elapsed = sw.ElapsedMilliseconds;
            int delay = (int)Math.Max(0, frameDurationMs - elapsed);
            if (delay > 0)
            {
                try { await Task.Delay(delay, ct); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    private void RaisePositionChanged(TimeSpan pos) =>
        PositionChanged?.Invoke(this, pos);

    private static int PositionToFrame(TimeSpan pos, int fps) =>
        (int)Math.Floor(pos.TotalSeconds * fps);

    private static TimeSpan FrameToPosition(int frame, int fps) =>
        fps > 0 ? TimeSpan.FromSeconds((double)frame / fps) : TimeSpan.Zero;

    private static TimeSpan Clamp(TimeSpan val, TimeSpan min, TimeSpan max) =>
        val < min ? min : val > max ? max : val;

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
