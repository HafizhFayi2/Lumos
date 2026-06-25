using System;
using System.Collections.Generic;
using System.Linq;
using Palmier.Domain;

namespace Palmier.Application.Commands;

public sealed class RemoveClipsAsyncCommand : TimelineCommand
{
    private readonly List<string> _clipIds;

    public override string Label => "Remove Clips";

    public RemoveClipsAsyncCommand(IEnumerable<string> clipIds)
    {
        _clipIds = new List<string>(clipIds);
    }

    protected override void Apply(Timeline timeline, CommandContext context)
    {
        var idsToRemove = new HashSet<string>(_clipIds);

        foreach (var track in timeline.Tracks)
            foreach (var clip in track.Clips)
                if (idsToRemove.Contains(clip.Id) && !string.IsNullOrEmpty(clip.LinkGroupId))
                {
                    var linked = timeline.Tracks
                        .SelectMany(t => t.Clips)
                        .Where(c => c.LinkGroupId == clip.LinkGroupId)
                        .Select(c => c.Id);
                    foreach (var lid in linked) idsToRemove.Add(lid);
                }

        foreach (var track in timeline.Tracks)
            track.Clips.RemoveAll(c => idsToRemove.Contains(c.Id));
    }
}
