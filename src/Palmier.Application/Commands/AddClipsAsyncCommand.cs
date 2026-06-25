using System;
using System.Collections.Generic;
using System.Linq;
using Palmier.Domain;

namespace Palmier.Application.Commands;

public sealed class AddClipsAsyncCommand : TimelineCommand
{
    private readonly List<Asset> _assets;
    private readonly string _trackId;
    private readonly int _startFrame;
    private readonly string? _linkedAudioTrackId;

    public override string Label => "Add Clips";

    public AddClipsAsyncCommand(
        IEnumerable<Asset> assets,
        string trackId,
        int startFrame,
        string? linkedAudioTrackId = null)
    {
        _assets              = new List<Asset>(assets);
        _trackId             = trackId;
        _startFrame          = startFrame;
        _linkedAudioTrackId  = linkedAudioTrackId;
    }

    protected override void Apply(Timeline timeline, CommandContext context)
    {
        var targetTrack = timeline.Tracks.FirstOrDefault(t => t.Id == _trackId);
        if (targetTrack == null) return;

        Track? audioTrack = null;
        if (!string.IsNullOrEmpty(_linkedAudioTrackId))
            audioTrack = timeline.Tracks.FirstOrDefault(t => t.Id == _linkedAudioTrackId);

        int currentStart = _startFrame;

        foreach (var asset in _assets)
        {
            context.ThrowIfCancelled();

            int durationFrames = asset.Duration > 0
                ? (int)Math.Round(asset.Duration * timeline.Fps)
                : 5 * timeline.Fps;

            ApplyOverwrites(targetTrack, currentStart, currentStart + durationFrames);
            if (audioTrack != null)
                ApplyOverwrites(audioTrack, currentStart, currentStart + durationFrames);

            var linkGroupId = Guid.NewGuid().ToString();
            var primaryClip = new Clip
            {
                Id             = Guid.NewGuid().ToString(),
                MediaRef       = asset.FilePath,
                MediaType      = asset.Type,
                SourceClipType = asset.Type,
                StartFrame     = currentStart,
                DurationFrames = durationFrames,
            };

            if (audioTrack != null)
            {
                primaryClip.LinkGroupId = linkGroupId;
                targetTrack.Clips.Add(primaryClip);

                audioTrack.Clips.Add(new Clip
                {
                    Id             = Guid.NewGuid().ToString(),
                    MediaRef       = asset.FilePath,
                    MediaType      = ClipType.Audio,
                    SourceClipType = asset.Type,
                    StartFrame     = currentStart,
                    DurationFrames = durationFrames,
                    LinkGroupId    = linkGroupId
                });
            }
            else
            {
                targetTrack.Clips.Add(primaryClip);
            }

            currentStart += durationFrames;
        }

        targetTrack.Clips = targetTrack.Clips.OrderBy(c => c.StartFrame).ToList();
        if (audioTrack != null)
            audioTrack.Clips = audioTrack.Clips.OrderBy(c => c.StartFrame).ToList();
    }

    private static void ApplyOverwrites(Track track, int start, int end)
    {
        var actions = OverwriteEngine.ComputeOverwrite(track.Clips, start, end);
        foreach (var action in actions)
        {
            switch (action)
            {
                case OverwriteAction.Remove remove:
                    track.Clips.RemoveAll(c => c.Id == remove.ClipId);
                    break;

                case OverwriteAction.TrimEnd trimEnd:
                    var tcEnd = track.Clips.FirstOrDefault(c => c.Id == trimEnd.ClipId);
                    tcEnd?.SetDuration(trimEnd.NewDuration);
                    break;

                case OverwriteAction.TrimStart trimStart:
                    var tcStart = track.Clips.FirstOrDefault(c => c.Id == trimStart.ClipId);
                    if (tcStart != null)
                    {
                        tcStart.StartFrame     = trimStart.NewStartFrame;
                        tcStart.TrimStartFrame = trimStart.NewTrimStart;
                        tcStart.SetDuration(trimStart.NewDuration);
                    }
                    break;

                case OverwriteAction.Split split:
                    var tcSplit = track.Clips.FirstOrDefault(c => c.Id == split.ClipId);
                    if (tcSplit != null)
                    {
                        int rightSource = (int)Math.Round(split.RightDuration * tcSplit.Speed);

                        var right = tcSplit.Clone(newId: true);
                        right.Id             = split.RightId;
                        right.StartFrame     = split.RightStartFrame;
                        right.DurationFrames = split.RightDuration;
                        right.TrimStartFrame = split.RightTrimStart;
                        right.FadeInFrames   = 0;
                        right.ClampFadesToDuration();
                        right.ClampKeyframesToDuration();

                        tcSplit.DurationFrames = split.LeftDuration;
                        tcSplit.TrimEndFrame  += rightSource;
                        tcSplit.FadeOutFrames  = 0;
                        tcSplit.ClampFadesToDuration();
                        tcSplit.ClampKeyframesToDuration();

                        track.Clips.Add(right);
                    }
                    break;
            }
        }
    }
}
