using Lumos.Domain;

namespace Lumos.Application;

public class MoveClipCommand : ICommand
{
    private readonly Timeline _timeline;
    private readonly string _clipId;
    private readonly int _newStartFrame;
    private readonly string _newTrackId;

    private Timeline? _beforeSnapshot;
    private Timeline? _afterSnapshot;

    public MoveClipCommand(Timeline timeline, string clipId, int newStartFrame, string newTrackId)
    {
        _timeline = timeline;
        _clipId = clipId;
        _newStartFrame = newStartFrame;
        _newTrackId = newTrackId;
    }

    public void Execute()
    {
        if (_afterSnapshot != null)
        {
            RestoreSnapshot(_afterSnapshot);
            return;
        }

        _beforeSnapshot = _timeline.Clone();
        PerformMove();
        _afterSnapshot = _timeline.Clone();
    }

    public void Undo()
    {
        if (_beforeSnapshot != null)
        {
            RestoreSnapshot(_beforeSnapshot);
        }
    }

    private void RestoreSnapshot(Timeline snapshot)
    {
        _timeline.Tracks.Clear();
        foreach (var t in snapshot.Tracks)
        {
            _timeline.Tracks.Add(t.Clone());
        }
        _timeline.Fps = snapshot.Fps;
        _timeline.Width = snapshot.Width;
        _timeline.Height = snapshot.Height;
        _timeline.SettingsConfigured = snapshot.SettingsConfigured;
    }

    private void PerformMove()
    {
        Track? sourceTrack = null;
        Clip? clipToMove = null;

        foreach (var t in _timeline.Tracks)
        {
            var c = t.Clips.FirstOrDefault(x => x.Id == _clipId);
            if (c != null)
            {
                sourceTrack = t;
                clipToMove = c;
                break;
            }
        }

        if (sourceTrack == null || clipToMove == null) return;

        var destTrack = _timeline.Tracks.FirstOrDefault(t => t.Id == _newTrackId);
        if (destTrack == null) return;

        // Verify compatibility
        if (destTrack.Type != clipToMove.MediaType)
        {
            if (destTrack.Type == ClipType.Video && clipToMove.MediaType == ClipType.Audio) return;
            if (destTrack.Type == ClipType.Audio && clipToMove.MediaType == ClipType.Video) return;
        }

        // Remove from source track
        sourceTrack.Clips.Remove(clipToMove);

        // Clear region in destination track
        var actions = OverwriteEngine.ComputeOverwrite(destTrack.Clips, _newStartFrame, _newStartFrame + clipToMove.DurationFrames);
        ApplyOverwriteActions(destTrack, actions);

        // Move to destination track
        clipToMove.StartFrame = _newStartFrame;
        destTrack.Clips.Add(clipToMove);

        // Sort clips on affected tracks
        sourceTrack.Clips = sourceTrack.Clips.OrderBy(c => c.StartFrame).ToList();
        destTrack.Clips = destTrack.Clips.OrderBy(c => c.StartFrame).ToList();
    }

    private void ApplyOverwriteActions(Track track, List<OverwriteAction> actions)
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
                    if (tcEnd != null)
                    {
                        tcEnd.SetDuration(trimEnd.NewDuration);
                    }
                    break;

                case OverwriteAction.TrimStart trimStart:
                    var tcStart = track.Clips.FirstOrDefault(c => c.Id == trimStart.ClipId);
                    if (tcStart != null)
                    {
                        tcStart.StartFrame = trimStart.NewStartFrame;
                        tcStart.TrimStartFrame = trimStart.NewTrimStart;
                        tcStart.SetDuration(trimStart.NewDuration);
                    }
                    break;

                case OverwriteAction.Split split:
                    var tcSplit = track.Clips.FirstOrDefault(c => c.Id == split.ClipId);
                    if (tcSplit != null)
                    {
                        int leftSource = (int)Math.Round(split.LeftDuration * tcSplit.Speed);
                        int rightSource = (int)Math.Round(split.RightDuration * tcSplit.Speed);

                        // Create right part
                        var right = tcSplit.Clone(newId: true);
                        right.Id = split.RightId;
                        right.StartFrame = split.RightStartFrame;
                        right.DurationFrames = split.RightDuration;
                        right.TrimStartFrame = split.RightTrimStart;
                        right.FadeInFrames = 0;
                        right.ClampFadesToDuration();
                        right.ClampKeyframesToDuration();

                        // Split keyframes
                        right.OpacityTrack = SplitTrack(tcSplit.OpacityTrack, split.LeftDuration, new AnimDouble(tcSplit.Opacity));
                        right.VolumeTrack = SplitTrack(tcSplit.VolumeTrack, split.LeftDuration, new AnimDouble(tcSplit.Volume));
                        right.PositionTrack = SplitTrack(tcSplit.PositionTrack, split.LeftDuration, new AnimPair(0, 0));
                        right.ScaleTrack = SplitTrack(tcSplit.ScaleTrack, split.LeftDuration, new AnimPair(1, 1));
                        right.RotationTrack = SplitTrack(tcSplit.RotationTrack, split.LeftDuration, new AnimDouble(tcSplit.Transform.Rotation));
                        right.CropTrack = SplitTrack(tcSplit.CropTrack, split.LeftDuration, tcSplit.Crop);

                        // Update left part
                        tcSplit.DurationFrames = split.LeftDuration;
                        tcSplit.TrimEndFrame += rightSource;
                        tcSplit.FadeOutFrames = 0;
                        tcSplit.ClampFadesToDuration();
                        tcSplit.ClampKeyframesToDuration();

                        tcSplit.OpacityTrack = SplitTrackLeft(tcSplit.OpacityTrack, split.LeftDuration);
                        tcSplit.VolumeTrack = SplitTrackLeft(tcSplit.VolumeTrack, split.LeftDuration);
                        tcSplit.PositionTrack = SplitTrackLeft(tcSplit.PositionTrack, split.LeftDuration);
                        tcSplit.ScaleTrack = SplitTrackLeft(tcSplit.ScaleTrack, split.LeftDuration);
                        tcSplit.RotationTrack = SplitTrackLeft(tcSplit.RotationTrack, split.LeftDuration);
                        tcSplit.CropTrack = SplitTrackLeft(tcSplit.CropTrack, split.LeftDuration);

                        track.Clips.Add(right);
                    }
                    break;
            }
        }
    }

    private KeyframeTrack<T>? SplitTrackLeft<T>(KeyframeTrack<T>? track, int splitOffset) where T : notnull, IKeyframeInterpolatable<T>
    {
        if (track == null) return null;
        var leftTrack = track.Clone();
        var toRemove = leftTrack.Keyframes.Where(k => k.Frame > splitOffset).ToList();
        foreach (var kf in toRemove)
        {
            leftTrack.Remove(kf.Frame);
        }
        return leftTrack;
    }

    private KeyframeTrack<T>? SplitTrack<T>(KeyframeTrack<T>? track, int splitOffset, T fallback) where T : notnull, IKeyframeInterpolatable<T>
    {
        if (track == null) return null;
        var rightTrack = new KeyframeTrack<T>();
        
        var boundaryValue = track.Sample(splitOffset, fallback);
        rightTrack.Upsert(new Keyframe<T>(0, boundaryValue));

        foreach (var kf in track.Keyframes.Where(k => k.Frame > splitOffset))
        {
            rightTrack.Upsert(new Keyframe<T>(kf.Frame - splitOffset, kf.Value, kf.InterpolationOut));
        }

        return rightTrack;
    }
}
