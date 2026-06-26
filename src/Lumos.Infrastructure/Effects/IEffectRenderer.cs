using Lumos.Domain;
using SkiaSharp;

namespace Lumos.Infrastructure.Effects;

public interface IEffectRenderer
{
    string EffectType { get; }
    SKBitmap Apply(SKBitmap source, Effect effect, int clipFrame);
}
