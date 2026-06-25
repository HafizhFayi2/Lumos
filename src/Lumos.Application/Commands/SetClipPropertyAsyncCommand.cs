using System;
using System.Collections.Generic;
using System.Linq;
using Palmier.Domain;

namespace Palmier.Application.Commands;

public sealed class SetClipPropertyAsyncCommand : TimelineCommand
{
    private readonly List<string> _clipIds;
    private readonly Action<Clip> _mutateAction;
    private readonly string _propertyLabel;

    public override string Label => _propertyLabel;

    public SetClipPropertyAsyncCommand(string clipId, Action<Clip> mutateAction, string propertyLabel = "Set Clip Property")
        : this(new List<string> { clipId }, mutateAction, propertyLabel) { }

    public SetClipPropertyAsyncCommand(IEnumerable<string> clipIds, Action<Clip> mutateAction, string propertyLabel = "Set Clip Property")
    {
        _clipIds       = new List<string>(clipIds);
        _mutateAction  = mutateAction;
        _propertyLabel = propertyLabel;
    }

    public override bool CanMergeWith(IAsyncCommand previous) =>
        previous is SetClipPropertyAsyncCommand other &&
        other._propertyLabel == _propertyLabel &&
        other._clipIds.SequenceEqual(_clipIds);

    public override IAsyncCommand MergeWith(IAsyncCommand previous) => this;

    protected override void Apply(Timeline timeline, CommandContext context)
    {
        var ids = new HashSet<string>(_clipIds);
        foreach (var track in timeline.Tracks)
            foreach (var clip in track.Clips)
                if (ids.Contains(clip.Id))
                    _mutateAction(clip);
    }
}
