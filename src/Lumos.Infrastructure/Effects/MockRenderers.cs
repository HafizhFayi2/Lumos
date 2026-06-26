using Lumos.Domain;
using SkiaSharp;

namespace Lumos.Infrastructure.Effects;

// TODO: Phase 8 - 100% Palmier Pro Feature Gap
// Tetrahedral 3D LUT interpolation requires loading .cube files.
// The math for 3D interpolation is complex and usually requires a parsed 3D table.
// Real implementation is deferred pending .cube file parser integration.
// Passes through until a LUT file path is supplied via the "lut_path" param.
public sealed class LUTTetraRenderer : IEffectRenderer
{
    public string EffectType => "lut_tetra";
    public SKBitmap Apply(SKBitmap s, Effect e, int c) => s.Copy();
}

