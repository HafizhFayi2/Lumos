using Lumos.Domain;
using System.Linq;

namespace Lumos.Application.Commands;

public sealed class RemoveEffectAsyncCommand : TimelineCommand
{
    private readonly string _clipId;
    private readonly string _effectId;
    private Effect? _removedEffect;
    private int _removedIndex = -1;

    public override string Label => "Remove Effect";

    public RemoveEffectAsyncCommand(string clipId, string effectId)
    {
        _clipId = clipId;
        _effectId = effectId;
    }

    protected override void Apply(Timeline timeline, CommandContext context)
    {
        var clip = timeline.Tracks.SelectMany(t => t.Clips).FirstOrDefault(c => c.Id == _clipId);
        if (clip == null) return;

        _removedIndex = clip.Effects.FindIndex(e => e.Id == _effectId);
        if (_removedIndex != -1)
        {
            _removedEffect = clip.Effects[_removedIndex];
            clip.Effects.RemoveAt(_removedIndex);
        }
    }
}
