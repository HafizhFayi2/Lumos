using Lumos.Domain;

namespace Lumos.Application;

public interface IPlaybackEngine : IDisposable
{
    void LoadTimeline(Timeline timeline);

    void Play();
    void Pause();
    void Stop();
    void Seek(TimeSpan position);
    void StepForward();
    void StepBackward();

    bool   IsPlaying       { get; }
    TimeSpan CurrentPosition { get; }
    TimeSpan Duration        { get; }

    /// Raised on UI thread each time the playhead advances.
    event EventHandler<TimeSpan> PositionChanged;

    /// Raised when a new composited frame is ready.
    event EventHandler<(int frame, byte[] data)>? FrameReady;
}
