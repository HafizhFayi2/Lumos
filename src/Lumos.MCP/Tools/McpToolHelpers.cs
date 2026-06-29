using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lumos.MCP.Tools;

/// <summary>
/// Shared JSON validation and response helpers for MCP tool implementations.
/// Eliminates duplicated TryGetString/TryGetInt/Error methods across tool classes.
/// </summary>
public static class McpToolHelpers
{
    public static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ── JSON response builders ───────────────────────────────────────────────

    public static string Ok() =>
        JsonSerializer.Serialize(new { ok = true }, DefaultJsonOptions);

    public static string Ok(object payload) =>
        JsonSerializer.Serialize(new { ok = true, data = payload }, DefaultJsonOptions);

    public static string Error(string message) =>
        JsonSerializer.Serialize(new { ok = false, error = message }, DefaultJsonOptions);

    // ── Argument extraction ──────────────────────────────────────────────────

    public static bool TryGetString(JsonElement el, string key, out string? value)
    {
        value = null;
        if (el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String)
        { value = p.GetString(); return true; }
        return false;
    }

    public static bool TryGetInt(JsonElement el, string key, out int value)
    {
        value = 0;
        if (el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out value)) return true;
        return false;
    }

    public static bool TryGetDouble(JsonElement el, string key, out double value)
    {
        value = 0;
        if (el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out value)) return true;
        return false;
    }

    public static bool TryGetStringArray(JsonElement el, string key, out List<string>? value)
    {
        value = null;
        if (!el.TryGetProperty(key, out var p) || p.ValueKind != JsonValueKind.Array) return false;
        value = p.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                  .Select(x => x.GetString()!).ToList();
        return true;
    }

    // ── Validation helpers ───────────────────────────────────────────────────

    public static string? ValidateClipId(JsonElement args, out string? clipId)
    {
        if (!TryGetString(args, "clip_id", out clipId) || string.IsNullOrWhiteSpace(clipId))
            return "clip_id (string) is required and must not be empty";
        return null;
    }

    public static string? ValidateNonEmptyClipIds(JsonElement args, out List<string>? ids)
    {
        if (!TryGetStringArray(args, "clip_ids", out ids) || ids is null || ids.Count == 0)
            return "clip_ids (non-empty array of strings) is required";
        return null;
    }

    public static string? ValidateFrameRange(int frame, int minFrame, int maxFrame, string paramName = "frame")
    {
        if (frame < minFrame) return $"{paramName} ({frame}) must be >= {minFrame}";
        if (maxFrame > 0 && frame > maxFrame) return $"{paramName} ({frame}) must be <= {maxFrame}";
        return null;
    }

    public static string? ValidateEffectType(string effectType)
    {
        string[] validEffects = [
            "color_grade", "chroma_key", "clarity", "glow", "grain",
            "grade_curves", "highlights_shadows", "lut_tetra", "levels",
            "color_wheels", "vignette"
        ];
        if (!validEffects.Contains(effectType))
            return $"effect_type '{effectType}' is not valid. Must be one of: {string.Join(", ", validEffects)}";
        return null;
    }
}
