using System;
using System.IO;
using System.Linq;
using Lumos.Domain;
using NAudio.Wave;

namespace Lumos.Media;

public sealed class TimelineAudioPlayer : IDisposable
{
    private WaveOutEvent? _output;
    private MediaFoundationReader? _reader;
    private string? _activeClipId;

    public void Play(Timeline? timeline, int timelineFrame)
    {
        var clip = FindAudibleClip(timeline, timelineFrame);
        if (clip == null)
        {
            Stop();
            return;
        }

        if (_activeClipId == clip.Id && _output?.PlaybackState == NAudio.Wave.PlaybackState.Playing)
            return;

        StartClip(clip, timeline!.Fps, timelineFrame);
    }

    public void Sync(Timeline? timeline, int timelineFrame)
    {
        var clip = FindAudibleClip(timeline, timelineFrame);
        if (clip == null)
        {
            Stop();
            return;
        }

        if (_activeClipId != clip.Id || _output?.PlaybackState != NAudio.Wave.PlaybackState.Playing)
            StartClip(clip, timeline!.Fps, timelineFrame);
    }

    public void Stop()
    {
        _activeClipId = null;
        _output?.Stop();
        _output?.Dispose();
        _reader?.Dispose();
        _output = null;
        _reader = null;
    }

    private void StartClip(Clip clip, int fps, int timelineFrame)
    {
        Stop();
        if (!File.Exists(clip.MediaRef)) return;

        try
        {
            int localFrame = Math.Max(0, timelineFrame - clip.StartFrame);
            int sourceFrame = clip.TrimStartFrame + (int)Math.Round(localFrame * clip.Speed);
            double sourceSeconds = sourceFrame / (double)Math.Max(1, fps);

            _reader = new MediaFoundationReader(clip.MediaRef);
            if (_reader.TotalTime > TimeSpan.Zero)
                _reader.CurrentTime = TimeSpan.FromSeconds(Math.Min(sourceSeconds, _reader.TotalTime.TotalSeconds));

            _output = new WaveOutEvent();
            _output.Init(_reader);
            _output.Volume = (float)Math.Clamp(clip.Volume, 0, 1);
            _output.Play();
            _activeClipId = clip.Id;
        }
        catch
        {
            Stop();
        }
    }

    private static Clip? FindAudibleClip(Timeline? timeline, int timelineFrame)
    {
        if (timeline == null) return null;

        return timeline.Tracks
            .Where(track => !track.IsMuted)
            .SelectMany(track => track.Clips)
            .Where(clip => clip.Contains(timelineFrame))
            .Where(clip => clip.MediaType is ClipType.Audio or ClipType.Video)
            .FirstOrDefault(clip => File.Exists(clip.MediaRef));
    }

    public void Dispose() => Stop();
}
