using Lumos.Domain;
using SkiaSharp;

namespace Lumos.Infrastructure.Effects;

// Tetrahedral 3D LUT interpolation — real impl deferred pending .cube file loading.
// Passes through until a LUT file path is supplied via the "lut_path" param.
public sealed class LUTTetraRenderer : IEffectRenderer
{
    public string EffectType => "lut_tetra";
    public SKBitmap Apply(SKBitmap s, Effect e, int c) => s.Copy();
}

