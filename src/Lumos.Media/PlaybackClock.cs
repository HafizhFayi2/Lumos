using System;
using System.Diagnostics;

namespace Lumos.Media;

/// Drift-corrected playback clock. Uses Stopwatch for high-resolution per-frame
/// timing but adjusts against DateTime.UtcNow every 120 frames to prevent
/// cumulative timer drift that could cause A/V desync over long playback.
/// DateTime.UtcNow uses a different timebase (system clock) than Stopwatch (QPC),
/// so comparing them reveals real drift rather than comparing QPC against itself.
public class PlaybackClock
{
    private readonly Stopwatch _stopwatch = new();
    private int _startFrame;
    private double _fps;
    private int _correctionCount;
    private DateTime _referenceTime;
    private DateTime _lastSyncTime;

    public PlaybackClock()
    {
        _referenceTime = DateTime.UtcNow;
        _lastSyncTime = _referenceTime;
    }

    public void Start(int currentFrame, double fps)
    {
        _startFrame = currentFrame;
        _fps = fps;
        _correctionCount = 0;
        _stopwatch.Restart();
        _referenceTime = DateTime.UtcNow;
        _lastSyncTime = _referenceTime;
    }

    public void Stop()
    {
        _stopwatch.Stop();
    }

    public int GetCurrentFrame()
    {
        if (!_stopwatch.IsRunning) return _startFrame;

        double stopwatchSeconds = _stopwatch.Elapsed.TotalSeconds;

        // Every 120 frames, correct Stopwatch drift against DateTime.UtcNow.
        // The system clock (DateTime.UtcNow) and high-perf counter (Stopwatch)
        // use different underlying hardware and can drift relative to each other.
        _correctionCount++;
        if (_correctionCount >= 120)
        {
            _correctionCount = 0;
            var now = DateTime.UtcNow;
            double referenceElapsed = (now - _referenceTime).TotalSeconds;
            double stopwatchElapsed = stopwatchSeconds;

            // If drift exceeds 1 frame duration, apply correction
            double frameDuration = _fps > 0 ? 1.0 / _fps : 0.033;
            double drift = stopwatchElapsed - referenceElapsed;

            if (Math.Abs(drift) > frameDuration)
            {
                // Reset reference to now and let Stopwatch continue
                // The correction effectively skips or repeats a frame
                _referenceTime = now;
                _stopwatch.Restart();
            }

            _lastSyncTime = now;
        }

        return _startFrame + (int)Math.Round(stopwatchSeconds * _fps);
    }
}
