using Lumos.Domain;
using SkiaSharp;

namespace Lumos.Infrastructure.Effects;

/// Tetrahedral 3D LUT interpolation renderer.
/// Loads .cube files and applies per-pixel tetrahedral interpolation
/// matching the algorithm in palmier-pro/Metal/LUTTetra.metal.
public sealed class LUTTetraRenderer : IEffectRenderer
{
    public string EffectType => "lut_tetra";

    // Thread-safe LRU-ish cache of parsed LUTs
    private static readonly Dictionary<string, float[,,]> _lutCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _cacheLock = new();

    public SKBitmap Apply(SKBitmap source, Effect effect, int clipFrame)
    {
        string lutPath = effect.Params.GetValueOrDefault("lut_path")?.StringValue ?? string.Empty;
        if (string.IsNullOrEmpty(lutPath) || !File.Exists(lutPath))
            return source.Copy();

        float[,,]? lut = LoadLut(lutPath);
        if (lut == null)
            return source.Copy();

        double intensity = effect.Params.GetValueOrDefault("intensity")?.NumericValue ?? 1.0;
        intensity = Math.Clamp(intensity, 0.0, 1.0);

        return ApplyTetra(source, lut, (float)intensity);
    }

    private static float[,,]? LoadLut(string path)
    {
        lock (_cacheLock)
        {
            if (_lutCache.TryGetValue(path, out var cached))
                return cached;
        }

        var lut = CubeFileParser.Parse(path);
        if (lut != null)
        {
            lock (_cacheLock)
                _lutCache[path] = lut;
        }
        return lut;
    }

    private static SKBitmap ApplyTetra(SKBitmap source, float[,,] lut, float intensity)
    {
        int size = lut.GetLength(0);
        float scale = (size - 1) / 255f;

        var dst = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);

        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                var px = source.GetPixel(x, y);

                float ri = px.Red   * scale;
                float gi = px.Green * scale;
                float bi = px.Blue  * scale;

                int r0 = (int)ri;
                int g0 = (int)gi;
                int b0 = (int)bi;

                // Clamp to [0, size-2] so b0+1 is always valid
                r0 = Math.Clamp(r0, 0, size - 2);
                g0 = Math.Clamp(g0, 0, size - 2);
                b0 = Math.Clamp(b0, 0, size - 2);

                float fr = ri - r0;
                float fg = gi - g0;
                float fb = bi - b0;

                // Fetch the 8 cube corners
                var c000 = Fetch(lut, size, b0,     g0,     r0);
                var c100 = Fetch(lut, size, b0,     g0,     r0 + 1);
                var c010 = Fetch(lut, size, b0,     g0 + 1, r0);
                var c110 = Fetch(lut, size, b0,     g0 + 1, r0 + 1);
                var c001 = Fetch(lut, size, b0 + 1, g0,     r0);
                var c101 = Fetch(lut, size, b0 + 1, g0,     r0 + 1);
                var c011 = Fetch(lut, size, b0 + 1, g0 + 1, r0);
                var c111 = Fetch(lut, size, b0 + 1, g0 + 1, r0 + 1);

                float outR, outG, outB;

                // Tetrahedral interpolation (mirrors Metal LUTTetra.metal)
                if (fr >= fg)
                {
                    if (fg >= fb)
                    {
                        // fr >= fg >= fb
                        outR = (1 - fr) * c000.r + (fr - fg) * c100.r + (fg - fb) * c110.r + fb * c111.r;
                        outG = (1 - fr) * c000.g + (fr - fg) * c100.g + (fg - fb) * c110.g + fb * c111.g;
                        outB = (1 - fr) * c000.b + (fr - fg) * c100.b + (fg - fb) * c110.b + fb * c111.b;
                    }
                    else if (fr >= fb)
                    {
                        // fr >= fb >= fg
                        outR = (1 - fr) * c000.r + (fr - fb) * c100.r + (fb - fg) * c101.r + fg * c111.r;
                        outG = (1 - fr) * c000.g + (fr - fb) * c100.g + (fb - fg) * c101.g + fg * c111.g;
                        outB = (1 - fr) * c000.b + (fr - fb) * c100.b + (fb - fg) * c101.b + fg * c111.b;
                    }
                    else
                    {
                        // fb >= fr >= fg
                        outR = (1 - fb) * c000.r + (fb - fr) * c001.r + (fr - fg) * c101.r + fg * c111.r;
                        outG = (1 - fb) * c000.g + (fb - fr) * c001.g + (fr - fg) * c101.g + fg * c111.g;
                        outB = (1 - fb) * c000.b + (fb - fr) * c001.b + (fr - fg) * c101.b + fg * c111.b;
                    }
                }
                else
                {
                    if (fb >= fg)
                    {
                        // fb >= fg >= fr
                        outR = (1 - fb) * c000.r + (fb - fg) * c001.r + (fg - fr) * c011.r + fr * c111.r;
                        outG = (1 - fb) * c000.g + (fb - fg) * c001.g + (fg - fr) * c011.g + fr * c111.g;
                        outB = (1 - fb) * c000.b + (fb - fg) * c001.b + (fg - fr) * c011.b + fr * c111.b;
                    }
                    else if (fb >= fr)
                    {
                        // fg >= fb >= fr
                        outR = (1 - fg) * c000.r + (fg - fb) * c010.r + (fb - fr) * c011.r + fr * c111.r;
                        outG = (1 - fg) * c000.g + (fg - fb) * c010.g + (fb - fr) * c011.g + fr * c111.g;
                        outB = (1 - fg) * c000.b + (fg - fb) * c010.b + (fb - fr) * c011.b + fr * c111.b;
                    }
                    else
                    {
                        // fg >= fr >= fb
                        outR = (1 - fg) * c000.r + (fg - fr) * c010.r + (fr - fb) * c110.r + fb * c111.r;
                        outG = (1 - fg) * c000.g + (fg - fr) * c010.g + (fr - fb) * c110.g + fb * c111.g;
                        outB = (1 - fg) * c000.b + (fg - fr) * c010.b + (fr - fb) * c110.b + fb * c111.b;
                    }
                }

                // Blend with original by intensity (matches Metal's mix())
                byte finalR = intensity >= 1.0f
                    ? (byte)Math.Clamp(outR * 255, 0, 255)
                    : (byte)Math.Clamp(Lerp(px.Red / 255f, outR, intensity) * 255, 0, 255);
                byte finalG = intensity >= 1.0f
                    ? (byte)Math.Clamp(outG * 255, 0, 255)
                    : (byte)Math.Clamp(Lerp(px.Green / 255f, outG, intensity) * 255, 0, 255);
                byte finalB = intensity >= 1.0f
                    ? (byte)Math.Clamp(outB * 255, 0, 255)
                    : (byte)Math.Clamp(Lerp(px.Blue / 255f, outB, intensity) * 255, 0, 255);

                dst.SetPixel(x, y, new SKColor(finalR, finalG, finalB, px.Alpha));
            }
        }

        return dst;
    }

    /// Fetch an RGB triple from the LUT at position (b, g, r), clamped to valid range.
    private static (float r, float g, float b) Fetch(float[,,] lut, int size, int b, int g, int r)
    {
        r = Math.Clamp(r, 0, size - 1);
        g = Math.Clamp(g, 0, size - 1);
        b = Math.Clamp(b, 0, size - 1);
        return (lut[b, g, r * 3], lut[b, g, r * 3 + 1], lut[b, g, r * 3 + 2]);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
