using System;
using System.Collections.Generic;
using System.Linq;
using Palmier.Domain;

namespace Palmier.Application.Commands;

public sealed class MoveClipAsyncCommand : TimelineCommand
{
    private readonly string _clipId;
    private readonly int _newStartFrame;
    private readonly string _newTrackId;

    public override string Label => "Move Clip";

    public MoveClipAsyncCommand(string clipId, int newStartFrame, string newTrackId)
    {
        _clipId        = clipId;
        _newStartFrame = newStartFrame;
        _newTrackId    = newTrackId;
    }

    protected override void Apply(Timeline timeline, CommandContext context)
    {
        Track? sourceTrack = null;
        Clip? clipToMove = null;

        foreach (var t in timeline.Tracks)
        {
            var c = t.Clips.FirstOrDefault(x => x.Id == _clipId);
            if (c != null) { sourceTrack = t; clipToMove = c; break; }
        }

        if (sourceTrack == null || clipToMove == null) return;

        var destTrack = timeline.Tracks.FirstOrDefault(t => t.Id == _newTrackId);
        if (destTrack == null) return;

        if (!clipToMove.MediaType.IsCompatible(destTrack.Type)) return;

        sourceTrack.Clips.Remove(clipToMove);

        var actions = OverwriteEngine.ComputeOverwrite(
            destTrack.Clips, _newStartFrame, _newStartFrame + clipToMove.DurationFrames);
        ApplyOverwriteActions(destTrack, actions);

        clipToMove.StartFrame = _newStartFrame;
        destTrack.Clips.Add(clipToMove);

        sourceTrack.Clips = sourceTrack.Clips.OrderBy(c => c.StartFrame).ToList();
        destTrack.Clips   = destTrack.Clips.OrderBy(c => c.StartFrame).ToList();
    }

    private static void ApplyOverwriteActions(Track track, List<OverwriteAction> actions)
    {
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

                        SplitKeyframes(tcSplit, right, split.LeftDuration);

                        tcSplit.DurationFrames = split.LeftDuration;
                        tcSplit.TrimEndFrame  += rightSource;
                        tcSplit.FadeOutFrames  = 0;
                        tcSplit.ClampFadesToDuration();
                        tcSplit.ClampKeyframesToDuration();

                        TruncateKeyframesLeft(tcSplit, split.LeftDuration);

                        track.Clips.Add(right);
                    }
                    break;
            }
        }
    }

    private static void SplitKeyframes(Clip source, Clip right, int splitOffset)
    {
        right.OpacityTrack  = SplitTrack(source.OpacityTrack, splitOffset, new AnimDouble(source.Opacity));
        right.VolumeTrack   = SplitTrack(source.VolumeTrack, splitOffset, new AnimDouble(source.Volume));
        right.PositionTrack = SplitTrack(source.PositionTrack, splitOffset, new AnimPair(0, 0));
        right.ScaleTrack    = SplitTrack(source.ScaleTrack, splitOffset, new AnimPair(1, 1));
        right.RotationTrack = SplitTrack(source.RotationTrack, splitOffset, new AnimDouble(source.Transform.Rotation));
        right.CropTrack     = SplitTrack(source.CropTrack, splitOffset, source.Crop);
    }

    private static void TruncateKeyframesLeft(Clip clip, int splitOffset)
    {
        clip.OpacityTrack  = SplitTrackLeft(clip.OpacityTrack, splitOffset);
        clip.VolumeTrack   = SplitTrackLeft(clip.VolumeTrack, splitOffset);
        clip.PositionTrack = SplitTrackLeft(clip.PositionTrack, splitOffset);
        clip.ScaleTrack    = SplitTrackLeft(clip.ScaleTrack, splitOffset);
        clip.RotationTrack = SplitTrackLeft(clip.RotationTrack, splitOffset);
        clip.CropTrack     = SplitTrackLeft(clip.CropTrack, splitOffset);
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
