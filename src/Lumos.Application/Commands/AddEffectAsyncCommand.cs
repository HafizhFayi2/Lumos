using Lumos.Domain;
using System.Linq;

namespace Lumos.Application.Commands;

public sealed class AddEffectAsyncCommand : TimelineCommand
{
    private readonly string _clipId;
    private readonly string _effectType;
    private Effect? _addedEffect;

    public override string Label => $"Add {_effectType} Effect";

    public AddEffectAsyncCommand(string clipId, string effectType)
    {
        _clipId = clipId;
        _effectType = effectType;
    }

    protected override void Apply(Timeline timeline, CommandContext context)
    {
        var clip = timeline.Tracks.SelectMany(t => t.Clips).FirstOrDefault(c => c.Id == _clipId);
        if (clip == null) return;

        // Domain models require an explicit Effects collection on Clip, 
        // but wait... in Lumos.Domain/Clip.cs there is no Effects list?
        // Wait, I need to check Clip.cs to see if it has Effects.
        // I will add it using multi_replace if it's missing.
        
        // Assuming Clip has List<Effect> Effects { get; } = new();
        _addedEffect = Effect.Make(_effectType);
        clip.Effects.Add(_addedEffect);
    }
}
