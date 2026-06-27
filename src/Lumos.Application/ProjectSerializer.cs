using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lumos.Domain;

namespace Lumos.Application;

/// <summary>
/// Serializes and deserializes .lumos project packages.
/// Package structure:
///   project.lumos/
///     project.json   — timeline, project metadata
///     media.json     — media manifest (asset paths, types, durations)
///     media/         — copied media files
/// </summary>
public sealed class ProjectSerializer
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ── Save ────────────────────────────────────────────────────────────────

    public void Save(string projectDir, ProjectData data)
    {
        Directory.CreateDirectory(projectDir);

        // Write project.json
        string projectPath = Path.Combine(projectDir, ProjectDefaults.TimelineFilename);
        string projectJson = JsonSerializer.Serialize(data.Project, JsonOpts);
        File.WriteAllText(projectPath, projectJson);

        // Write media.json
        string mediaPath = Path.Combine(projectDir, ProjectDefaults.ManifestFilename);
        string mediaJson = JsonSerializer.Serialize(data.Manifest, JsonOpts);
        File.WriteAllText(mediaPath, mediaJson);
    }

    // ── Load ────────────────────────────────────────────────────────────────

    public ProjectData? Load(string projectDir)
    {
        string projectPath = Path.Combine(projectDir, ProjectDefaults.TimelineFilename);
        string mediaPath = Path.Combine(projectDir, ProjectDefaults.ManifestFilename);

        if (!File.Exists(projectPath))
            return null;

        Project project;
        try
        {
            string projectJson = File.ReadAllText(projectPath);
            project = JsonSerializer.Deserialize<Project>(projectJson, JsonOpts)
                      ?? new Project();
        }
        catch
        {
            return null;
        }

        MediaManifest manifest;
        if (File.Exists(mediaPath))
        {
            try
            {
                string mediaJson = File.ReadAllText(mediaPath);
                manifest = JsonSerializer.Deserialize<MediaManifest>(mediaJson, JsonOpts)
                           ?? new MediaManifest();
            }
            catch
            {
                manifest = new MediaManifest();
            }
        }
        else
        {
            manifest = new MediaManifest();
        }

        return new ProjectData(project, manifest);
    }

    // ── Data types ──────────────────────────────────────────────────────────

    public sealed record ProjectData(Project Project, MediaManifest Manifest);
}

/// <summary>
/// Media manifest tracking all imported assets for a project.
/// </summary>
public sealed class MediaManifest
{
    public List<MediaEntry> Entries { get; set; } = new();
}

public sealed class MediaEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OriginalPath { get; set; } = string.Empty;
    public string ProjectRelativePath { get; set; } = string.Empty;
    public ClipType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Duration { get; set; }
    public int? SourceWidth { get; set; }
    public int? SourceHeight { get; set; }
    public double? SourceFps { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public bool IsPresent { get; set; } = true;
}
