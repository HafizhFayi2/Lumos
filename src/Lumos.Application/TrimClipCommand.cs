using Lumos.Domain;

namespace Lumos.Application;

public class TrimClipCommand : ICommand
{
    private readonly Timeline _timeline;
    private readonly string _clipId;
    private readonly int _newTrimStartFrame;
    private readonly int _newTrimEndFrame;

    private Timeline? _beforeSnapshot;
    private Timeline? _afterSnapshot;

    public TrimClipCommand(Timeline timeline, string clipId, int newTrimStartFrame, int newTrimEndFrame)
    {
        _timeline = timeline;
        _clipId = clipId;
        _newTrimStartFrame = newTrimStartFrame;
        _newTrimEndFrame = newTrimEndFrame;
    }

    public void Execute()
    {
        if (_afterSnapshot != null)
        {
            RestoreSnapshot(_afterSnapshot);
            return;
        }

        _beforeSnapshot = _timeline.Clone();
        PerformTrim();
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

    private void PerformTrim()
    {
        Track? track = null;
        Clip? clip = null;

        foreach (var t in _timeline.Tracks)
        {
            var c = t.Clips.FirstOrDefault(x => x.Id == _clipId);
            if (c != null)
            {
                track = t;
                clip = c;
                break;
            }
        }

        if (track == null || clip == null) return;

        int prevStart = clip.TrimStartFrame;
        int prevEnd = clip.TrimEndFrame;
        int prevDuration = clip.DurationFrames;

        int deltaStartSource = _newTrimStartFrame - prevStart;
        int deltaEndSource = _newTrimEndFrame - prevEnd;

        int deltaStartTimeline = (int)Math.Round(deltaStartSource / clip.Speed);
        int deltaEndTimeline = (int)Math.Round(deltaEndSource / clip.Speed);

        int newDuration = prevDuration - deltaStartTimeline - deltaEndTimeline;
        int newStartFrame = clip.StartFrame + deltaStartTimeline;

        clip.TrimStartFrame = _newTrimStartFrame;
        clip.TrimEndFrame = _newTrimEndFrame;
        clip.StartFrame = newStartFrame;
        clip.SetDuration(newDuration);

        track.Clips = track.Clips.OrderBy(c => c.StartFrame).ToList();
    }
}
