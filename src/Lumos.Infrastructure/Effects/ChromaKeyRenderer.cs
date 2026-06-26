using Lumos.Domain;
using SkiaSharp;

namespace Lumos.Infrastructure.Effects;

/// Chroma key: replaces pixels within HSV distance of key colour with transparency.
public sealed class ChromaKeyRenderer : IEffectRenderer
{
    public string EffectType => "chroma_key";

    public SKBitmap Apply(SKBitmap source, Effect effect, int clipFrame)
    {
        float keyR     = (float)(effect.Params.GetValueOrDefault("key_r")?.Resolve(clipFrame, 0)   ?? 0.0);
        float keyG     = (float)(effect.Params.GetValueOrDefault("key_g")?.Resolve(clipFrame, 255) ?? 255.0);
        float keyB     = (float)(effect.Params.GetValueOrDefault("key_b")?.Resolve(clipFrame, 0)   ?? 0.0);
        float threshold = (float)(effect.Params.GetValueOrDefault("threshold")?.Resolve(clipFrame, 0.35) ?? 0.35);

        SKColor keyColor = new((byte)Math.Clamp(keyR, 0, 255),
                               (byte)Math.Clamp(keyG, 0, 255),
                               (byte)Math.Clamp(keyB, 0, 255));
        ToHsv(keyColor, out float kH, out float kS, out float kV);

        var dst = source.Copy();
        for (int y = 0; y < dst.Height; y++)
        {
            for (int x = 0; x < dst.Width; x++)
            {
                var px = dst.GetPixel(x, y);
                ToHsv(px, out float pH, out float pS, out float pV);

                float hDiff = Math.Min(Math.Abs(pH - kH), 360 - Math.Abs(pH - kH)) / 180f;
                float sDiff = Math.Abs(pS - kS);
                float dist  = MathF.Sqrt(hDiff * hDiff + sDiff * sDiff * 0.5f);

                if (dist < threshold)
                    dst.SetPixel(x, y, SKColors.Transparent);
            }
        }
        return dst;
    }

    private static void ToHsv(SKColor c, out float h, out float s, out float v)
    {
        float r = c.Red / 255f, g = c.Green / 255f, b = c.Blue / 255f;
        float max = MathF.Max(r, MathF.Max(g, b));
        float min = MathF.Min(r, MathF.Min(g, b));
        float delta = max - min;

        v = max;
        s = max < 1e-6f ? 0 : delta / max;

        if (delta < 1e-6f) { h = 0; return; }
        if (max == r) h = 60f * (((g - b) / delta) % 6);
        else if (max == g) h = 60f * ((b - r) / delta + 2);
        else h = 60f * ((r - g) / delta + 4);
        if (h < 0) h += 360;
    }
}
