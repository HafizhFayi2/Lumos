using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lumos.Desktop;

/// Persistent application settings stored as JSON in %APPDATA%/LumosDesktop/settings.json.
public sealed class LumosSettings
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LumosDesktop");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly string ApiKeysPath = Path.Combine(SettingsDir, "api_keys.dat");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ── Settings fields ─────────────────────────────────────────────────

    /// Last known window position and size.
    public WindowSettings? Window { get; set; }

    /// Most recently opened project directory.
    public string? LastProjectPath { get; set; }

    /// Whether to auto-open the last project on startup.
    public bool AutoOpenLastProject { get; set; } = true;

    /// User's preferred preview quality.
    public string PreviewQuality { get; set; } = "Half";

    /// Whether to check for updates on startup.
    public bool CheckForUpdates { get; set; } = true;

    /// Previously configured generation model API keys (persisted encrypted via DPAPI).
    [JsonIgnore]
    public Dictionary<string, string> ModelApiKeys { get; set; } = new();

    /// Previously configured generation model endpoints (persisted encrypted via DPAPI).
    [JsonIgnore]
    public Dictionary<string, string> ModelEndpoints { get; set; } = new();

    // ── Load / Save ─────────────────────────────────────────────────────

    public static LumosSettings Load()
    {
        var settings = new LumosSettings();
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                settings = JsonSerializer.Deserialize<LumosSettings>(json, JsonOpts) ?? new LumosSettings();
            }
        }
        catch
        {
            // Corrupted settings — return defaults
        }

        // Load encrypted API keys
        settings.LoadApiKeys();
        return settings;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(this, JsonOpts);
            File.WriteAllText(SettingsPath, json);
            SaveApiKeys();
        }
        catch
        {
            // Settings save is non-critical
        }
    }

    /// Get the LumosDesktop app data directory (ensures it exists).
    public static string GetAppDataDir()
    {
        Directory.CreateDirectory(SettingsDir);
        return SettingsDir;
    }

    // ── Encrypted API key persistence (DPAPI) ───────────────────────────

    private void LoadApiKeys()
    {
        if (!File.Exists(ApiKeysPath)) return;
        try
        {
            byte[] encrypted = File.ReadAllBytes(ApiKeysPath);
            byte[] decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(decrypted);
            var data = JsonSerializer.Deserialize<ApiKeyStore>(json, JsonOpts);
            if (data != null)
            {
                ModelApiKeys = data.Keys ?? new();
                ModelEndpoints = data.Endpoints ?? new();
            }
        }
        catch
        {
            // Decryption failed — keys stay empty
        }
    }

    private void SaveApiKeys()
    {
        try
        {
            var data = new ApiKeyStore { Keys = ModelApiKeys, Endpoints = ModelEndpoints };
            var json = JsonSerializer.Serialize(data, JsonOpts);
            byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(ApiKeysPath, encrypted);
        }
        catch
        {
            // API key persistence is non-critical
        }
    }

    public void SetApiKey(string modelId, string apiKey)
    {
        try
        {
            ModelApiKeys[modelId] = apiKey;
            SaveApiKeys();
        }
        catch
        {
            // Non-critical
        }
    }

    private sealed record ApiKeyStore
    {
        public Dictionary<string, string>? Keys { get; init; }
        public Dictionary<string, string>? Endpoints { get; init; }
    }
}

/// Window position and size for restoring on next launch.
public sealed record WindowSettings
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; } = 1400;
    public double Height { get; init; } = 900;
    public bool Maximized { get; init; }
}
