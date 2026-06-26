using Lumos.Domain;
using SkiaSharp;

namespace Lumos.Infrastructure.Effects;

/// Lifts shadows and pulls down highlights via a per-channel curve approximated with a matrix + gamma.
public sealed class HighlightsShadowsRenderer : IEffectRenderer
{
    public string EffectType => "highlights_shadows";

    public SKBitmap Apply(SKBitmap source, Effect effect, int clipFrame)
    {
        float highlights = (float)(effect.Params.GetValueOrDefault("highlights")?.Resolve(clipFrame, 0) ?? 0.0);
        float shadows    = (float)(effect.Params.GetValueOrDefault("shadows")?.Resolve(clipFrame, 0) ?? 0.0);

        // Clamp to [-1, 1]
        highlights = Math.Clamp(highlights, -1f, 1f);
        shadows    = Math.Clamp(shadows, -1f, 1f);

        // Build a 256-entry LUT that curves highlights down and shadows up.
        byte[] lut = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            float t = i / 255f;

            // Shadows: boost values in [0, 0.5] range
            float shadowBoost = shadows > 0
                ? shadows * (1f - t) * (1f - t) * 2f   // lift toe
                : shadows * (1f - t) * t * 2f;          // pull down lower-mids

            // Highlights: affect values in [0.5, 1.0] range
            float highlightPull = highlights < 0
                ? highlights * t * t * 2f               // pull down highlights
                : highlights * t * (1f - t) * 2f;       // open up mids

            float v = Math.Clamp(t + shadowBoost + highlightPull, 0f, 1f);
            lut[i] = (byte)(v * 255f + 0.5f);
        }

        // Apply LUT via table-lookup on raw pixels (safer than unsafe ptr on ReadOnly spans)
        var dst = source.Copy();
        for (int y = 0; y < dst.Height; y++)
        {
            for (int x = 0; x < dst.Width; x++)
            {
                var c = dst.GetPixel(x, y);
                dst.SetPixel(x, y, new SKColor(lut[c.Red], lut[c.Green], lut[c.Blue], c.Alpha));
            }
        }
        return dst;
    }
}
