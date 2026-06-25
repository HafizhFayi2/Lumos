using Lumos.Domain;

namespace Lumos.Application;

public class RemoveClipsCommand : ICommand
{
    private readonly Timeline _timeline;
    private readonly List<string> _clipIds;

    private Timeline? _beforeSnapshot;
    private Timeline? _afterSnapshot;

    public RemoveClipsCommand(Timeline timeline, List<string> clipIds)
    {
        _timeline = timeline;
        _clipIds = clipIds;
    }

    public void Execute()
    {
        if (_afterSnapshot != null)
        {
            RestoreSnapshot(_afterSnapshot);
            return;
        }

        _beforeSnapshot = _timeline.Clone();
        PerformRemove();
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

    private void PerformRemove()
    {
        var idsToRemove = new HashSet<string>(_clipIds);
        var partners = new List<string>();

        foreach (var track in _timeline.Tracks)
        {
            foreach (var clip in track.Clips)
            {
                if (idsToRemove.Contains(clip.Id) && !string.IsNullOrEmpty(clip.LinkGroupId))
                {
                    var linked = _timeline.Tracks
                        .SelectMany(t => t.Clips)
                        .Where(c => c.LinkGroupId == clip.LinkGroupId)
                        .Select(c => c.Id);
                    partners.AddRange(linked);
                }
            }
        }

        foreach (var partnerId in partners)
        {
            idsToRemove.Add(partnerId);
        }

        foreach (var track in _timeline.Tracks)
        {
            track.Clips.RemoveAll(c => idsToRemove.Contains(c.Id));
        }
    }
}
