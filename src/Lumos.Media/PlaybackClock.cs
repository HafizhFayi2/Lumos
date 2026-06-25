using System;
using System.Diagnostics;

namespace Lumos.Media;

public class PlaybackClock
{
    private readonly Stopwatch _stopwatch = new();
    private int _startFrame;
    private double _fps;

    public void Start(int currentFrame, double fps)
    {
        _startFrame = currentFrame;
        _fps = fps;
        _stopwatch.Restart();
    }

    public void Stop()
    {
        _stopwatch.Stop();
    }

    public int GetCurrentFrame()
    {
        if (!_stopwatch.IsRunning) return _startFrame;
        double elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
        return _startFrame + (int)Math.Round(elapsedSeconds * _fps);
    }
}
