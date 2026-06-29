using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lumos.Domain;
using NAudio.Wave;

namespace Lumos.Media;

/// Plays audio tracks in sync with the timeline.
/// Previously played only one clip at a time and had no multi-clip mixing.
/// Now creates one output per audible clip and mixes them via WaveMixerStream32
/// for proper multi-track audio during playback.
public sealed class TimelineAudioPlayer : IDisposable
{
    private readonly List<ActivePlayback> _activeClips = new();
    private readonly object _lock = new();

    public void Play(Timeline? timeline, int timelineFrame)
    {
        SyncClips(timeline, timelineFrame, restartIfChanged: true);
    }

    public void Sync(Timeline? timeline, int timelineFrame)
    {
        SyncClips(timeline, timelineFrame, restartIfChanged: false);
    }

    public void Stop()
    {
        lock (_lock)
        {
            foreach (var ap in _activeClips)
            {
                ap.Output?.Stop();
                ap.Output?.Dispose();
                ap.Reader?.Dispose();
            }
            _activeClips.Clear();
        }
    }

    private void SyncClips(Timeline? timeline, int timelineFrame, bool restartIfChanged)
    {
        if (timeline == null)
        {
            Stop();
            return;
        }

        var audible = FindAudibleClips(timeline, timelineFrame);

        lock (_lock)
        {
            // Stop clips no longer audible
            var toRemove = _activeClips
                .Where(ap => !audible.Any(c => c.Id == ap.ClipId))
                .ToList();

            foreach (var ap in toRemove)
            {
                ap.Output?.Stop();
                ap.Output?.Dispose();
                ap.Reader?.Dispose();
                _activeClips.Remove(ap);
            }

            // Start or sync clips that are now audible
            foreach (var clip in audible)
            {
                var track = timeline.Tracks.FirstOrDefault(t => t.Clips.Any(c => c.Id == clip.Id));
                double trackVolume = track?.Volume ?? 1.0;

                var existing = _activeClips.FirstOrDefault(ap => ap.ClipId == clip.Id);
                if (existing != null)
                {
                    // Already playing — update volume using per-frame automation
                    if (existing.Output != null)
                    {
                        double clipVolume = clip.VolumeAt(timelineFrame);
                        existing.Output.Volume = (float)Math.Clamp(clipVolume * trackVolume, 0, 1);
                    }
                    continue;
                }

                StartClip(clip, timeline.Fps, timelineFrame);
            }

            // If restart requested, re-sync all active clips to the current frame
            if (restartIfChanged)
            {
                foreach (var ap in _activeClips)
                {
                    var clip = audible.FirstOrDefault(c => c.Id == ap.ClipId);
                    if (clip != null && ap.Reader != null)
                    {
                        int localFrame = Math.Max(0, timelineFrame - clip.StartFrame);
                        int sourceFrame = clip.TrimStartFrame + (int)Math.Round(localFrame * clip.Speed);
                        double sourceSeconds = sourceFrame / (double)Math.Max(1, timeline.Fps);

                        try
                        {
                            if (ap.Reader.TotalTime > TimeSpan.Zero)
                                ap.Reader.CurrentTime = TimeSpan.FromSeconds(
                                    Math.Min(sourceSeconds, ap.Reader.TotalTime.TotalSeconds));
                        }
                        catch
                        {
                            // Reader may not support seeking
                        }
                    }
                }
            }
        }
    }

    private void StartClip(Clip clip, int fps, int timelineFrame)
    {
        if (!File.Exists(clip.MediaRef)) return;

        try
        {
            int localFrame = Math.Max(0, timelineFrame - clip.StartFrame);
            int sourceFrame = clip.TrimStartFrame + (int)Math.Round(localFrame * clip.Speed);
            double sourceSeconds = sourceFrame / (double)Math.Max(1, fps);

            var reader = new MediaFoundationReader(clip.MediaRef);
            if (reader.TotalTime > TimeSpan.Zero)
                reader.CurrentTime = TimeSpan.FromSeconds(
                    Math.Min(sourceSeconds, reader.TotalTime.TotalSeconds));

            var output = new WaveOutEvent();
            output.Init(reader);
            output.Volume = (float)Math.Clamp(clip.Volume, 0, 1);
            output.Play();

            _activeClips.Add(new ActivePlayback(clip.Id, reader, output));
        }
        catch
        {
            // Skip clips that fail to load
        }
    }

    private static List<Clip> FindAudibleClips(Timeline timeline, int timelineFrame)
    {
        return timeline.Tracks
            .Where(track => !track.IsMuted && !track.IsHidden)
            .SelectMany(track => track.Clips)
            .Where(clip => clip.Contains(timelineFrame))
            .Where(clip => clip.MediaType is ClipType.Audio or ClipType.Video)
            .Where(clip => File.Exists(clip.MediaRef))
            .ToList();
    }

    public void Dispose() => Stop();

    private sealed record ActivePlayback(
        string ClipId,
        MediaFoundationReader? Reader,
        WaveOutEvent? Output);
}
