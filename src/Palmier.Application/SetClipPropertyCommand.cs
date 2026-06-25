using Palmier.Domain;

namespace Palmier.Application;

public class SetClipPropertyCommand : ICommand
{
    private readonly Timeline _timeline;
    private readonly List<string> _clipIds;
    private readonly Action<Clip> _mutateAction;

    private Timeline? _beforeSnapshot;
    private Timeline? _afterSnapshot;

    public SetClipPropertyCommand(Timeline timeline, List<string> clipIds, Action<Clip> mutateAction)
    {
        _timeline = timeline;
        _clipIds = clipIds;
        _mutateAction = mutateAction;
    }

    public SetClipPropertyCommand(Timeline timeline, string clipId, Action<Clip> mutateAction)
        : this(timeline, new List<string> { clipId }, mutateAction)
    {
    }

    public void Execute()
    {
        if (_afterSnapshot != null)
        {
            RestoreSnapshot(_afterSnapshot);
            return;
        }

        _beforeSnapshot = _timeline.Clone();
        PerformMutation();
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

    private void PerformMutation()
    {
        var ids = new HashSet<string>(_clipIds);
        foreach (var track in _timeline.Tracks)
        {
            foreach (var clip in track.Clips)
            {
                if (ids.Contains(clip.Id))
                {
                    _mutateAction(clip);
                }
            }
        }
    }
}
