using Lumos.Domain;

namespace Lumos.Application;

public class SplitClipCommand : ICommand
{
    private readonly Timeline _timeline;
    private readonly string _clipId;
    private readonly int _splitFrame;
    
    private Timeline? _beforeSnapshot;
    private Timeline? _afterSnapshot;

    public SplitClipCommand(Timeline timeline, string clipId, int splitFrame)
    {
        _timeline = timeline;
        _clipId = clipId;
        _splitFrame = splitFrame;
    }

    public void Execute()
    {
        if (_afterSnapshot != null)
        {
            RestoreSnapshot(_afterSnapshot);
            return;
        }

        _beforeSnapshot = _timeline.Clone();
        PerformSplit();
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

    private void PerformSplit()
    {
        Track? targetTrack = null;
        Clip? targetClip = null;

        foreach (var t in _timeline.Tracks)
        {
            var c = t.Clips.FirstOrDefault(clip => clip.Id == _clipId);
            if (c != null)
            {
                targetTrack = t;
                targetClip = c;
                break;
            }
        }

        if (targetTrack == null || targetClip == null) return;
        if (_splitFrame <= targetClip.StartFrame || _splitFrame >= targetClip.EndFrame) return;

        var clipIdsToSplit = new List<string> { _clipId };
        if (!string.IsNullOrEmpty(targetClip.LinkGroupId))
        {
            var partners = _timeline.Tracks
                .SelectMany(t => t.Clips)
                .Where(c => c.Id != _clipId && c.LinkGroupId == targetClip.LinkGroupId)
                .Select(c => c.Id);
            clipIdsToSplit.AddRange(partners);
        }

        var newRightClips = new List<Clip>();
        var newLinkGroupId = Guid.NewGuid().ToString();
        bool useNewLinkGroup = clipIdsToSplit.Count > 1;

        foreach (var id in clipIdsToSplit)
        {
            var splitResult = SplitSingleClipInTimeline(id, _splitFrame);
            if (splitResult != null)
            {
                newRightClips.Add(splitResult);
            }
        }

        if (useNewLinkGroup)
        {
            foreach (var clip in newRightClips)
            {
                clip.LinkGroupId = newLinkGroupId;
            }
        }
    }

    private Clip? SplitSingleClipInTimeline(string id, int atFrame)
    {
        Track? track = null;
        Clip? clip = null;

        foreach (var t in _timeline.Tracks)
        {
            var c = t.Clips.FirstOrDefault(x => x.Id == id);
            if (c != null)
            {
                track = t;
                clip = c;
                break;
            }
        }

        if (track == null || clip == null) return null;
        if (atFrame <= clip.StartFrame || atFrame >= clip.EndFrame) return null;

        int splitOffset = atFrame - clip.StartFrame;
        int leftSource = (int)Math.Round(splitOffset * clip.Speed);
        int rightSource = (int)Math.Round((clip.DurationFrames - splitOffset) * clip.Speed);

        // Update left clip in place
        var left = clip;
        left.DurationFrames = splitOffset;
        left.TrimEndFrame += rightSource;
        left.FadeOutFrames = 0;
        left.ClampFadesToDuration();
        left.ClampKeyframesToDuration();

        // Create right clip
        var right = clip.Clone(newId: true);
        right.StartFrame = atFrame;
        right.DurationFrames = clip.DurationFrames - splitOffset;
        right.TrimStartFrame += leftSource;
        right.FadeInFrames = 0;
        right.ClampFadesToDuration();
        right.ClampKeyframesToDuration();

        // Split keyframe tracks
        right.OpacityTrack = SplitTrack(clip.OpacityTrack, splitOffset, new AnimDouble(clip.Opacity));
        left.OpacityTrack = SplitTrackLeft(clip.OpacityTrack, splitOffset);

        right.VolumeTrack = SplitTrack(clip.VolumeTrack, splitOffset, new AnimDouble(clip.Volume));
        left.VolumeTrack = SplitTrackLeft(clip.VolumeTrack, splitOffset);

        right.PositionTrack = SplitTrack(clip.PositionTrack, splitOffset, new AnimPair(0, 0));
        left.PositionTrack = SplitTrackLeft(clip.PositionTrack, splitOffset);

        right.ScaleTrack = SplitTrack(clip.ScaleTrack, splitOffset, new AnimPair(1, 1));
        left.ScaleTrack = SplitTrackLeft(clip.ScaleTrack, splitOffset);

        right.RotationTrack = SplitTrack(clip.RotationTrack, splitOffset, new AnimDouble(clip.Transform.Rotation));
        left.RotationTrack = SplitTrackLeft(clip.RotationTrack, splitOffset);

        right.CropTrack = SplitTrack(clip.CropTrack, splitOffset, clip.Crop);
        left.CropTrack = SplitTrackLeft(clip.CropTrack, splitOffset);

        track.Clips.Add(right);
        track.Clips = track.Clips.OrderBy(c => c.StartFrame).ToList();

        return right;
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
