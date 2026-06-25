using Lumos.Domain;
using System;
using System.Collections.ObjectModel;

namespace Lumos.Desktop.ViewModels;

public class TimelineViewModel : ViewModelBase
{
    private Timeline _timeline = new();
    public Timeline Timeline
    {
        get => _timeline;
        set
        {
            if (SetProperty(ref _timeline, value))
            {
                UpdateTracks();
            }
        }
    }

    public ObservableCollection<Track> Tracks { get; } = new();

    private double _zoomScale = Defaults.PixelsPerFrame;
    public double ZoomScale
    {
        get => _zoomScale;
        set => SetProperty(ref _zoomScale, value);
    }

    private TimeSpan _playheadPosition;
    public TimeSpan PlayheadPosition
    {
        get => _playheadPosition;
        set => SetProperty(ref _playheadPosition, value);
    }

    private void UpdateTracks()
    {
        Tracks.Clear();
        foreach (var track in _timeline.Tracks)
        {
            Tracks.Add(track);
        }
    }
}
