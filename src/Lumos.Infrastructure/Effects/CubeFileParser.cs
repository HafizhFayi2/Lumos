using System.Globalization;

namespace Lumos.Infrastructure.Effects;

/// Parses industry-standard .cube 3D LUT files (IRIDAS/Adobe spec).
/// Supports LUT_3D_SIZE headers, TITLE, comments, and data lines.
/// Returns a float[,,] table indexed as [blue, green, red * 3 + channel].
public static class CubeFileParser
{
    /// Parse a .cube file from disk.
    /// Returns null if the file cannot be parsed.
    public static float[,,]? Parse(string path)
    {
        try
        {
            return ParseInternal(File.ReadLines(path));
        }
        catch
        {
            return null;
        }
    }

    /// Parse .cube content from an enumerable of text lines.
    public static float[,,] ParseInternal(IEnumerable<string> lines)
    {
        int size = 0;
        var entries = new List<(float r, float g, float b)>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            if (line.StartsWith("TITLE", StringComparison.OrdinalIgnoreCase))
                continue; // Skip title — metadata only

            if (line.StartsWith("LUT_3D_SIZE", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && int.TryParse(parts[1], out var n) && n > 0 && n <= 256)
                    size = n;
                continue;
            }

            // Skip 1D shaper LUT if present — we only handle the 3D LUT
            if (line.StartsWith("LUT_1D", StringComparison.OrdinalIgnoreCase))
                continue;

            // Skip range declarations
            if (line.StartsWith("LUT_3D_INPUT_RANGE", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("LUT_1D_INPUT_RANGE", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("LUT_IN_VIDEO_RANGE", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("LUT_OUT_VIDEO_RANGE", StringComparison.OrdinalIgnoreCase))
                continue;

            // Attempt to parse as a data line: three floats
            var dataParts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (dataParts.Length == 3 &&
                float.TryParse(dataParts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var r) &&
                float.TryParse(dataParts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var g) &&
                float.TryParse(dataParts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
            {
                entries.Add((r, g, b));
            }
        }

        if (size == 0)
        {
            // Infer grid size from the cube root of entry count
            size = (int)Math.Round(Math.Pow(entries.Count, 1.0 / 3.0));
            if (size * size * size != entries.Count)
                throw new InvalidDataException(
                    $"Cannot determine LUT size from {entries.Count} entries.");
        }

        int expected = size * size * size;
        if (entries.Count < expected)
            throw new InvalidDataException(
                $"Expected {expected} entries for {size}³ LUT, got {entries.Count}.");

        // Store as [blue, green, red * 3 + channel] matching LutRenderer convention
        var table = new float[size, size, size * 3];

        // .cube ordering: R changes fastest, then G, then B
        for (int i = 0; i < expected; i++)
        {
            int rIdx = i % size;
            int gIdx = (i / size) % size;
            int bIdx = i / (size * size);

            table[bIdx, gIdx, rIdx * 3]     = entries[i].r;
            table[bIdx, gIdx, rIdx * 3 + 1] = entries[i].g;
            table[bIdx, gIdx, rIdx * 3 + 2] = entries[i].b;
        }

        return table;
    }
}
