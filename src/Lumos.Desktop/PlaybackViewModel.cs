using System;
using Palmier.Application;
using Palmier.Domain;

namespace Palmier.Desktop.ViewModels;

public sealed class PlaybackViewModel : ViewModelBase, IDisposable
{
    private readonly IPlaybackEngine _engine;

    public PlaybackViewModel(IPlaybackEngine engine)
    {
        _engine = engine;
        _engine.PositionChanged += OnPositionChanged;
    }

    public void LoadTimeline(Timeline timeline) => _engine.LoadTimeline(timeline);

    // ── Commands ────────────────────────────────────────────────────────────

    public void Play()         => _engine.Play();
    public void Pause()        => _engine.Pause();
    public void Stop()         => _engine.Stop();
    public void StepForward()  => _engine.StepForward();
    public void StepBackward() => _engine.StepBackward();

    public void TogglePlay()
    {
        if (_engine.IsPlaying) _engine.Pause();
        else                   _engine.Play();
    }

    public void SeekTo(TimeSpan position) => _engine.Seek(position);

    // ── Observable state ────────────────────────────────────────────────────

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        private set => SetProperty(ref _isPlaying, value);
    }

    private TimeSpan _position;
    public TimeSpan Position
    {
        get => _position;
        private set
        {
            if (SetProperty(ref _position, value))
                OnPropertyChanged(nameof(PositionSeconds));
        }
    }

    private TimeSpan _duration;
    public TimeSpan Duration
    {
        get => _duration;
        private set
        {
            if (SetProperty(ref _duration, value))
                OnPropertyChanged(nameof(DurationSeconds));
        }
    }

    public double PositionSeconds => _position.TotalSeconds;
    public double DurationSeconds => _duration.TotalSeconds;

    public string TimeCode =>
        $"{(int)_position.TotalHours:D2}:{_position.Minutes:D2}:{_position.Seconds:D2}:{(int)(_position.Milliseconds / (1000.0 / 30)):D2}";

    // ── Internal ────────────────────────────────────────────────────────────

    private void OnPositionChanged(object? sender, TimeSpan pos)
    {
        // Already marshalled to UI thread by caller if Avalonia dispatcher used
        Position   = pos;
        IsPlaying  = _engine.IsPlaying;
        Duration   = _engine.Duration;
        OnPropertyChanged(nameof(TimeCode));
    }

    public void Dispose()
    {
        _engine.PositionChanged -= OnPositionChanged;
        _engine.Dispose();
    }
}
