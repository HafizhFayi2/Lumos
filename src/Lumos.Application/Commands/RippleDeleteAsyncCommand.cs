using System;
using System.Collections.Generic;
using System.Linq;
using Lumos.Domain;

namespace Lumos.Application.Commands;

public sealed class RippleDeleteAsyncCommand : TimelineCommand
{
    private readonly List<string> _clipIds;

    public override string Label => "Ripple Delete";

    public RippleDeleteAsyncCommand(IEnumerable<string> clipIds)
    {
        _clipIds = new List<string>(clipIds);
    }

    protected override void Apply(Timeline timeline, CommandContext context)
    {
        var idsToRemove = new HashSet<string>(_clipIds);

        foreach (var track in timeline.Tracks)
            foreach (var clip in track.Clips)
                if (idsToRemove.Contains(clip.Id) && !string.IsNullOrEmpty(clip.LinkGroupId))
                    foreach (var lid in timeline.Tracks.SelectMany(t => t.Clips)
                                 .Where(c => c.LinkGroupId == clip.LinkGroupId)
                                 .Select(c => c.Id))
                        idsToRemove.Add(lid);

        var globalRemovedRanges = timeline.Tracks
            .SelectMany(t => t.Clips)
            .Where(c => idsToRemove.Contains(c.Id))
            .Select(c => new FrameRange(c.StartFrame, c.EndFrame))
            .ToList();

        var shiftsByTrack = new Dictionary<Track, List<ClipShift>>();

        foreach (var track in timeline.Tracks)
        {
            bool hasOwnRemovals = track.Clips.Any(c => idsToRemove.Contains(c.Id));

            if (hasOwnRemovals)
            {
                shiftsByTrack[track] = RippleEngine.ComputeRippleShifts(track.Clips, idsToRemove);
            }
            else if (track.IsSyncLocked)
            {
                var shifts = RippleEngine.ComputeRippleShiftsForRanges(track.Clips, globalRemovedRanges);
                if (!ValidateShifts(track, shifts))
                    throw new InvalidOperationException("Ripple delete blocked by sync-lock collision.");
                shiftsByTrack[track] = shifts;
            }
        }

        foreach (var track in timeline.Tracks)
            track.Clips.RemoveAll(c => idsToRemove.Contains(c.Id));

        foreach (var (track, shifts) in shiftsByTrack)
        {
            var shiftMap = shifts.ToDictionary(s => s.ClipId, s => s.NewStartFrame);
            foreach (var clip in track.Clips)
                if (shiftMap.TryGetValue(clip.Id, out int newStart))
                    clip.StartFrame = newStart;
            track.Clips = track.Clips.OrderBy(c => c.StartFrame).ToList();
        }
    }

    private static bool ValidateShifts(Track track, List<ClipShift> shifts)
    {
        if (shifts.Count == 0) return true;
        var shiftMap = shifts.ToDictionary(s => s.ClipId, s => s.NewStartFrame);
        var intervals = track.Clips
            .Select(c => new FrameRange(
                shiftMap.TryGetValue(c.Id, out int sf) ? sf : c.StartFrame,
                (shiftMap.TryGetValue(c.Id, out int sf2) ? sf2 : c.StartFrame) + c.DurationFrames))
            .OrderBy(r => r.Start)
            .ToList();

        for (int i = 1; i < intervals.Count; i++)
            if (intervals[i].Start < intervals[i - 1].End)
                return false;
        return true;
    }
}
