using System;
using System.Collections.Generic;
using System.Linq;
using Lumos.Domain;

namespace Lumos.Application.Commands;

public sealed class SplitClipAsyncCommand : TimelineCommand
{
    private readonly string _clipId;
    private readonly int _splitFrame;

    public override string Label => "Split Clip";

    public SplitClipAsyncCommand(string clipId, int splitFrame)
    {
        _clipId     = clipId;
        _splitFrame = splitFrame;
    }

    protected override void Apply(Timeline timeline, CommandContext context)
    {
        Track? targetTrack = null;
        Clip? targetClip = null;

        foreach (var t in timeline.Tracks)
        {
            var c = t.Clips.FirstOrDefault(clip => clip.Id == _clipId);
            if (c != null) { targetTrack = t; targetClip = c; break; }
        }

        if (targetTrack == null || targetClip == null) return;
        if (_splitFrame <= targetClip.StartFrame || _splitFrame >= targetClip.EndFrame) return;

        var clipIdsToSplit = new List<string> { _clipId };
        if (!string.IsNullOrEmpty(targetClip.LinkGroupId))
        {
            var partners = timeline.Tracks
                .SelectMany(t => t.Clips)
                .Where(c => c.Id != _clipId && c.LinkGroupId == targetClip.LinkGroupId)
                .Select(c => c.Id);
            clipIdsToSplit.AddRange(partners);
        }

        var newRightClips = new List<Clip>();
        string newLinkGroupId = Guid.NewGuid().ToString();
        bool useNewLinkGroup = clipIdsToSplit.Count > 1;

        foreach (var id in clipIdsToSplit)
        {
            var right = SplitSingleClip(timeline, id, _splitFrame);
            if (right != null) newRightClips.Add(right);
        }

        if (useNewLinkGroup)
            foreach (var clip in newRightClips)
                clip.LinkGroupId = newLinkGroupId;
    }

    private static Clip? SplitSingleClip(Timeline timeline, string id, int atFrame)
    {
        Track? track = null;
        Clip? clip = null;

        foreach (var t in timeline.Tracks)
        {
            var c = t.Clips.FirstOrDefault(x => x.Id == id);
            if (c != null) { track = t; clip = c; break; }
        }

        if (track == null || clip == null) return null;
        if (atFrame <= clip.StartFrame || atFrame >= clip.EndFrame) return null;

        int splitOffset = atFrame - clip.StartFrame;
        int leftSource  = (int)Math.Round(splitOffset * clip.Speed);
        int rightSource = (int)Math.Round((clip.DurationFrames - splitOffset) * clip.Speed);

        int originalDuration = clip.DurationFrames;

        var left = clip;
        left.DurationFrames = splitOffset;
        left.TrimEndFrame  += rightSource;
        left.FadeOutFrames  = 0;
        left.ClampFadesToDuration();
        left.ClampKeyframesToDuration();

        var right = clip.Clone(newId: true);
        right.StartFrame      = atFrame;
        right.DurationFrames  = originalDuration - splitOffset;
        right.TrimStartFrame += leftSource;
        right.FadeInFrames    = 0;
        right.ClampFadesToDuration();
        right.ClampKeyframesToDuration();

        right.OpacityTrack  = SplitTrack(clip.OpacityTrack, splitOffset, new AnimDouble(clip.Opacity));
        left.OpacityTrack   = SplitTrackLeft(clip.OpacityTrack, splitOffset);
        right.VolumeTrack   = SplitTrack(clip.VolumeTrack, splitOffset, new AnimDouble(clip.Volume));
        left.VolumeTrack    = SplitTrackLeft(clip.VolumeTrack, splitOffset);
        right.PositionTrack = SplitTrack(clip.PositionTrack, splitOffset, new AnimPair(0, 0));
        left.PositionTrack  = SplitTrackLeft(clip.PositionTrack, splitOffset);
        right.ScaleTrack    = SplitTrack(clip.ScaleTrack, splitOffset, new AnimPair(1, 1));
        left.ScaleTrack     = SplitTrackLeft(clip.ScaleTrack, splitOffset);
        right.RotationTrack = SplitTrack(clip.RotationTrack, splitOffset, new AnimDouble(clip.Transform.Rotation));
        left.RotationTrack  = SplitTrackLeft(clip.RotationTrack, splitOffset);
        right.CropTrack     = SplitTrack(clip.CropTrack, splitOffset, clip.Crop);
        left.CropTrack      = SplitTrackLeft(clip.CropTrack, splitOffset);

        track.Clips.Add(right);
        track.Clips = track.Clips.OrderBy(c => c.StartFrame).ToList();

        return right;
    }

    private static KeyframeTrack<T>? SplitTrackLeft<T>(KeyframeTrack<T>? track, int splitOffset)
        where T : notnull, IKeyframeInterpolatable<T>
    {
        if (track == null) return null;
        var leftTrack = track.Clone();
        foreach (var kf in leftTrack.Keyframes.Where(k => k.Frame > splitOffset).ToList())
            leftTrack.Remove(kf.Frame);
        return leftTrack;
    }

    private static KeyframeTrack<T>? SplitTrack<T>(KeyframeTrack<T>? track, int splitOffset, T fallback)
        where T : notnull, IKeyframeInterpolatable<T>
    {
        if (track == null) return null;
        var rightTrack = new KeyframeTrack<T>();
        rightTrack.Upsert(new Keyframe<T>(0, track.Sample(splitOffset, fallback)));
        foreach (var kf in track.Keyframes.Where(k => k.Frame > splitOffset))
            rightTrack.Upsert(new Keyframe<T>(kf.Frame - splitOffset, kf.Value, kf.InterpolationOut));
        return rightTrack;
    }
}
