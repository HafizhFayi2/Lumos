using System;
using System.Linq;
using Lumos.Domain;

namespace Lumos.Application.Commands;

public sealed class TrimClipAsyncCommand : TimelineCommand
{
    private readonly string _clipId;
    private readonly int _newTrimStartFrame;
    private readonly int _newTrimEndFrame;

    public override string Label => "Trim Clip";

    public TrimClipAsyncCommand(string clipId, int newTrimStartFrame, int newTrimEndFrame)
    {
        _clipId            = clipId;
        _newTrimStartFrame = newTrimStartFrame;
        _newTrimEndFrame   = newTrimEndFrame;
    }

    public override bool CanMergeWith(IAsyncCommand previous) =>
        previous is TrimClipAsyncCommand other && other._clipId == _clipId;

    public override IAsyncCommand MergeWith(IAsyncCommand previous) => this;

    protected override void Apply(Timeline timeline, CommandContext context)
    {
        Track? track = null;
        Clip? clip = null;

        foreach (var t in timeline.Tracks)
        {
            var c = t.Clips.FirstOrDefault(x => x.Id == _clipId);
            if (c != null) { track = t; clip = c; break; }
        }

        if (track == null || clip == null) return;

        int prevStart    = clip.TrimStartFrame;
        int prevEnd      = clip.TrimEndFrame;
        int prevDuration = clip.DurationFrames;

        int deltaStartSource   = _newTrimStartFrame - prevStart;
        int deltaEndSource     = _newTrimEndFrame - prevEnd;
        int deltaStartTimeline = (int)Math.Round(deltaStartSource / clip.Speed);
        int deltaEndTimeline   = (int)Math.Round(deltaEndSource / clip.Speed);

        int newDuration   = prevDuration - deltaStartTimeline - deltaEndTimeline;
        int newStartFrame = clip.StartFrame + deltaStartTimeline;

        clip.TrimStartFrame = _newTrimStartFrame;
        clip.TrimEndFrame   = _newTrimEndFrame;
        clip.StartFrame     = newStartFrame;
        clip.SetDuration(newDuration);

        track.Clips = track.Clips.OrderBy(c => c.StartFrame).ToList();
    }
}
