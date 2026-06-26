using Lumos.Domain;
using SkiaSharp;

namespace Lumos.Infrastructure.Effects;

/// LUT renderer: loads a .cube 3D LUT and applies it per-pixel.
/// Falls back to identity (returns source unchanged) if file not found or parse fails.
public sealed class LutRenderer : IEffectRenderer
{
    public string EffectType => "lut";

    // In-memory cache of parsed LUTs
    private static readonly Dictionary<string, float[,,]> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _cacheLock = new();

    public SKBitmap Apply(SKBitmap source, Effect effect, int clipFrame)
    {
        string lutPath = effect.Params.GetValueOrDefault("lut_path")?.StringValue ?? string.Empty;
        if (string.IsNullOrEmpty(lutPath) || !File.Exists(lutPath))
            return source.Copy();

        float[,,]? lut = LoadLut(lutPath);
        if (lut == null) return source.Copy();

        return ApplyLut(source, lut);
    }

    private static float[,,]? LoadLut(string path)
    {
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(path, out var cached)) return cached;
        }
        try
        {
            var lut = ParseCube(path);
            lock (_cacheLock) _cache[path] = lut;
            return lut;
        }
        catch { return null; }
    }

    private static float[,,] ParseCube(string path)
    {
        int size = 0;
        var entries = new List<(float r, float g, float b)>();

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('#') || line.Length == 0) continue;
            if (line.StartsWith("LUT_3D_SIZE", StringComparison.OrdinalIgnoreCase))
            {
                size = int.Parse(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1]);
                continue;
            }
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 3 &&
                float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float r) &&
                float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float g) &&
                float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float b))
            {
                entries.Add((r, g, b));
            }
        }

        if (size == 0) size = (int)Math.Round(Math.Pow(entries.Count, 1.0 / 3.0));
        var table = new float[size, size, size * 3];

        for (int i = 0; i < Math.Min(entries.Count, size * size * size); i++)
        {
            int rb = i % size;
            int gb = (i / size) % size;
            int bb = i / (size * size);
            table[bb, gb, rb * 3 + 0] = entries[i].r;
            table[bb, gb, rb * 3 + 1] = entries[i].g;
            table[bb, gb, rb * 3 + 2] = entries[i].b;
        }
        return table;
    }

    private static SKBitmap ApplyLut(SKBitmap source, float[,,] lut)
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

                int r0 = (int)ri, g0 = (int)gi, b0 = (int)bi;
                r0 = Math.Clamp(r0, 0, size - 2);
                g0 = Math.Clamp(g0, 0, size - 2);
                b0 = Math.Clamp(b0, 0, size - 2);

                float fr = ri - r0, fg = gi - g0, fb = bi - b0;

                // Trilinear interpolation
                float outR = Trilinear(lut, b0, g0, r0, 0, fb, fg, fr, size);
                float outG = Trilinear(lut, b0, g0, r0, 1, fb, fg, fr, size);
                float outB = Trilinear(lut, b0, g0, r0, 2, fb, fg, fr, size);

                dst.SetPixel(x, y, new SKColor(
                    (byte)Math.Clamp(outR * 255, 0, 255),
                    (byte)Math.Clamp(outG * 255, 0, 255),
                    (byte)Math.Clamp(outB * 255, 0, 255),
                    px.Alpha));
            }
        }
        return dst;
    }

    private static float Trilinear(float[,,] lut, int b, int g, int r, int ch, float fb, float fg, float fr, int size)
    {
        int r1 = Math.Min(r + 1, size - 1);
        int g1 = Math.Min(g + 1, size - 1);
        int b1 = Math.Min(b + 1, size - 1);
        float c000 = lut[b,  g,  r  * 3 + ch];
        float c001 = lut[b,  g,  r1 * 3 + ch];
        float c010 = lut[b,  g1, r  * 3 + ch];
        float c011 = lut[b,  g1, r1 * 3 + ch];
        float c100 = lut[b1, g,  r  * 3 + ch];
        float c101 = lut[b1, g,  r1 * 3 + ch];
        float c110 = lut[b1, g1, r  * 3 + ch];
        float c111 = lut[b1, g1, r1 * 3 + ch];
        return c000 * (1-fb)*(1-fg)*(1-fr)
             + c001 * (1-fb)*(1-fg)*fr
             + c010 * (1-fb)*fg*(1-fr)
             + c011 * (1-fb)*fg*fr
             + c100 * fb*(1-fg)*(1-fr)
             + c101 * fb*(1-fg)*fr
             + c110 * fb*fg*(1-fr)
             + c111 * fb*fg*fr;
    }
}
