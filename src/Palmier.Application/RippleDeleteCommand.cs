using Palmier.Domain;

namespace Palmier.Application;

public class RippleDeleteCommand : ICommand
{
    private readonly Timeline _timeline;
    private readonly List<string> _clipIds;

    private Timeline? _beforeSnapshot;
    private Timeline? _afterSnapshot;

    public RippleDeleteCommand(Timeline timeline, List<string> clipIds)
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
        
        if (!PerformRippleDelete())
        {
            RestoreSnapshot(_beforeSnapshot);
            throw new InvalidOperationException("Ripple delete blocked by sync-lock collision.");
        }

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

    private bool PerformRippleDelete()
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

        var globalRemovedRanges = _timeline.Tracks
            .SelectMany(t => t.Clips)
            .Where(c => idsToRemove.Contains(c.Id))
            .Select(c => new FrameRange(c.StartFrame, c.EndFrame))
            .ToList();

        var shiftsByTrack = new Dictionary<Track, List<ClipShift>>();

        for (int i = 0; i < _timeline.Tracks.Count; i++)
        {
            var track = _timeline.Tracks[i];
            bool hasOwnRemovals = track.Clips.Any(c => idsToRemove.Contains(c.Id));

            if (hasOwnRemovals)
            {
                var shifts = RippleEngine.ComputeRippleShifts(track.Clips, idsToRemove);
                shiftsByTrack[track] = shifts;
            }
            else if (track.IsSyncLocked)
            {
                var shifts = RippleEngine.ComputeRippleShiftsForRanges(track.Clips, globalRemovedRanges);
                
                if (!ValidateShifts(track, shifts))
                {
                    return false;
                }
                shiftsByTrack[track] = shifts;
            }
        }

        foreach (var track in _timeline.Tracks)
        {
            track.Clips.RemoveAll(c => idsToRemove.Contains(c.Id));
        }

        foreach (var entry in shiftsByTrack)
        {
            var track = entry.Key;
            var shifts = entry.Value;
            
            var shiftMap = shifts.ToDictionary(s => s.ClipId, s => s.NewStartFrame);
            foreach (var clip in track.Clips)
            {
                if (shiftMap.TryGetValue(clip.Id, out int newStart))
                {
                    clip.StartFrame = newStart;
                }
            }

            track.Clips = track.Clips.OrderBy(c => c.StartFrame).ToList();
        }

        return true;
    }

    private bool ValidateShifts(Track track, List<ClipShift> shifts)
    {
        if (shifts.Count == 0) return true;

        var shiftMap = shifts.ToDictionary(s => s.ClipId, s => s.NewStartFrame);
        var intervals = new List<FrameRange>();

        foreach (var clip in track.Clips)
        {
            int start = shiftMap.TryGetValue(clip.Id, out int sFrame) ? sFrame : clip.StartFrame;
            if (start < 0) return false;
            intervals.Add(new FrameRange(start, start + clip.DurationFrames));
        }

        intervals = intervals.OrderBy(r => r.Start).ToList();
        for (int i = 1; i < intervals.Count; i++)
        {
            if (intervals[i].Start < intervals[i - 1].End)
            {
                return false;
            }
        }

        return true;
    }
}
