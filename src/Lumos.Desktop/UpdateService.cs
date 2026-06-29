using System.Xml.Linq;

namespace Lumos.Desktop;

/// Checks for application updates against the hosted appcast.xml manifest.
/// Runs on startup (if enabled) and can be triggered manually.
public static class UpdateService
{
    /// Remote URL where the appcast manifest is hosted.
    /// In development, falls back to bundled appcast.xml.
    public const string AppcastUrl = "https://raw.githubusercontent.com/lumos-io/lumos-desktop/main/appcast.xml";

    /// Current app version read from the assembly.
    public static Version CurrentVersion => typeof(UpdateService).Assembly.GetName().Version ?? new Version(1, 0, 0);

    /// Check for updates asynchronously.
    /// Returns an UpdateInfo if a newer version is available, null otherwise.
    public static async Task<UpdateInfo?> CheckForUpdateAsync(bool useFallback = false)
    {
        try
        {
            string xml;
            if (useFallback)
            {
                // Read bundled appcast.xml as fallback
                var localPath = Path.Combine(AppContext.BaseDirectory, "appcast.xml");
                if (!File.Exists(localPath)) return null;
                xml = await File.ReadAllTextAsync(localPath);
            }
            else
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var response = await http.GetAsync(AppcastUrl);
                response.EnsureSuccessStatusCode();
                xml = await response.Content.ReadAsStringAsync();
            }

            return ParseAppcast(xml);
        }
        catch
        {
            // Network errors, parse errors — silently skip update check
            return null;
        }
    }

    /// Parse the appcast XML into an UpdateInfo.
    private static UpdateInfo? ParseAppcast(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var root = doc.Root;
            if (root == null) return null;

            var versionStr = root.Element("version")?.Value;
            if (string.IsNullOrEmpty(versionStr)) return null;

            var latestVersion = Version.Parse(versionStr);
            if (latestVersion <= CurrentVersion) return null; // Already up to date

            return new UpdateInfo
            {
                Version = latestVersion,
                ReleaseDate = root.Element("releaseDate")?.Value ?? "Unknown",
                DownloadUrl = root.Element("downloadUrl")?.Value ?? string.Empty,
                Changelog = root.Element("changelog")?.Elements("change")
                    ?.Select(e => e.Value)
                    .Where(v => !string.IsNullOrEmpty(v))
                    .ToList() ?? new List<string>(),
            };
        }
        catch
        {
            return null;
        }
    }
}

/// Information about an available update.
public sealed record UpdateInfo
{
    public required Version Version { get; init; }
    public string ReleaseDate { get; init; } = "Unknown";
    public string DownloadUrl { get; init; } = string.Empty;
    public IReadOnlyList<string> Changelog { get; init; } = Array.Empty<string>();
}
