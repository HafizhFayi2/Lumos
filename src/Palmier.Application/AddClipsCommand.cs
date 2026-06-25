using Palmier.Domain;

namespace Palmier.Application;

public class AddClipsCommand : ICommand
{
    private readonly Timeline _timeline;
    private readonly List<Asset> _assets;
    private readonly string _trackId;
    private readonly int _startFrame;
    private readonly string? _linkedAudioTrackId;

    private Timeline? _beforeSnapshot;
    private Timeline? _afterSnapshot;

    public AddClipsCommand(Timeline timeline, List<Asset> assets, string trackId, int startFrame, string? linkedAudioTrackId = null)
    {
        _timeline = timeline;
        _assets = assets;
        _trackId = trackId;
        _startFrame = startFrame;
        _linkedAudioTrackId = linkedAudioTrackId;
    }

    public void Execute()
    {
        if (_afterSnapshot != null)
        {
            RestoreSnapshot(_afterSnapshot);
            return;
        }

        _beforeSnapshot = _timeline.Clone();
        PerformAdd();
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

    private void PerformAdd()
    {
        var targetTrack = _timeline.Tracks.FirstOrDefault(t => t.Id == _trackId);
        if (targetTrack == null) return;

        Track? audioTrack = null;
        if (!string.IsNullOrEmpty(_linkedAudioTrackId))
        {
            audioTrack = _timeline.Tracks.FirstOrDefault(t => t.Id == _linkedAudioTrackId);
        }

        int currentStart = _startFrame;

        foreach (var asset in _assets)
        {
            int durationFrames = asset.Duration > 0 
                ? (int)Math.Round(asset.Duration * _timeline.Fps) 
                : 5 * _timeline.Fps;

            var actions = OverwriteEngine.ComputeOverwrite(targetTrack.Clips, currentStart, currentStart + durationFrames);
            ApplyOverwriteActions(targetTrack, actions);

            if (audioTrack != null)
            {
                var audioActions = OverwriteEngine.ComputeOverwrite(audioTrack.Clips, currentStart, currentStart + durationFrames);
                ApplyOverwriteActions(audioTrack, audioActions);
            }

            var linkGroupId = Guid.NewGuid().ToString();

            var primaryClip = new Clip
            {
                Id = Guid.NewGuid().ToString(),
                MediaRef = asset.FilePath,
                MediaType = asset.Type,
                SourceClipType = asset.Type,
                StartFrame = currentStart,
                DurationFrames = durationFrames,
                TrimStartFrame = 0,
                TrimEndFrame = 0,
                Speed = 1.0,
                Volume = 1.0,
                Opacity = 1.0
            };

            if (audioTrack != null)
            {
                primaryClip.LinkGroupId = linkGroupId;
                targetTrack.Clips.Add(primaryClip);

                var linkedAudio = new Clip
                {
                    Id = Guid.NewGuid().ToString(),
                    MediaRef = asset.FilePath,
                    MediaType = ClipType.Audio,
                    SourceClipType = asset.Type,
                    StartFrame = currentStart,
                    DurationFrames = durationFrames,
                    TrimStartFrame = 0,
                    TrimEndFrame = 0,
                    Speed = 1.0,
                    Volume = 1.0,
                    Opacity = 1.0,
                    LinkGroupId = linkGroupId
                };
                audioTrack.Clips.Add(linkedAudio);
            }
            else
            {
                targetTrack.Clips.Add(primaryClip);
            }

            currentStart += durationFrames;
        }

        targetTrack.Clips = targetTrack.Clips.OrderBy(c => c.StartFrame).ToList();
        if (audioTrack != null)
        {
            audioTrack.Clips = audioTrack.Clips.OrderBy(c => c.StartFrame).ToList();
        }
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

                        var right = tcSplit.Clone(newId: true);
                        right.Id = split.RightId;
                        right.StartFrame = split.RightStartFrame;
                        right.DurationFrames = split.RightDuration;
                        right.TrimStartFrame = split.RightTrimStart;
                        right.FadeInFrames = 0;
                        right.ClampFadesToDuration();
                        right.ClampKeyframesToDuration();

                        right.OpacityTrack = SplitTrack(tcSplit.OpacityTrack, split.LeftDuration, new AnimDouble(tcSplit.Opacity));
                        right.VolumeTrack = SplitTrack(tcSplit.VolumeTrack, split.LeftDuration, new AnimDouble(tcSplit.Volume));
                        right.PositionTrack = SplitTrack(tcSplit.PositionTrack, split.LeftDuration, new AnimPair(0, 0));
                        right.ScaleTrack = SplitTrack(tcSplit.ScaleTrack, split.LeftDuration, new AnimPair(1, 1));
                        right.RotationTrack = SplitTrack(tcSplit.RotationTrack, split.LeftDuration, new AnimDouble(tcSplit.Transform.Rotation));
                        right.CropTrack = SplitTrack(tcSplit.CropTrack, split.LeftDuration, tcSplit.Crop);

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
