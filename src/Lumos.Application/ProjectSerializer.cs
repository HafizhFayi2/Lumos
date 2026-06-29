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
///     folders.json   — media folder hierarchy
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

        // Write folders.json
        string foldersPath = Path.Combine(projectDir, ProjectDefaults.FoldersFilename);
        string foldersJson = JsonSerializer.Serialize(data.Folders, JsonOpts);
        File.WriteAllText(foldersPath, foldersJson);
    }

    // ── Load ────────────────────────────────────────────────────────────────

    public ProjectData? Load(string projectDir)
    {
        string projectPath = Path.Combine(projectDir, ProjectDefaults.TimelineFilename);
        string mediaPath = Path.Combine(projectDir, ProjectDefaults.ManifestFilename);
        string foldersPath = Path.Combine(projectDir, ProjectDefaults.FoldersFilename);

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

        FolderManifest folders;
        if (File.Exists(foldersPath))
        {
            try
            {
                string foldersJson = File.ReadAllText(foldersPath);
                folders = JsonSerializer.Deserialize<FolderManifest>(foldersJson, JsonOpts)
                          ?? new FolderManifest();
            }
            catch
            {
                folders = new FolderManifest();
            }
        }
        else
        {
            folders = new FolderManifest();
        }

        return new ProjectData(project, manifest, folders);
    }

    // ── Data types ──────────────────────────────────────────────────────────

    public sealed record ProjectData(Project Project, MediaManifest Manifest, FolderManifest Folders);
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
    public string? FolderId { get; set; }
}

/// <summary>
/// Folder manifest tracking the media folder hierarchy for a project.
/// </summary>
public sealed class FolderManifest
{
    public List<FolderEntry> Folders { get; set; } = new();
}

/// <summary>
/// A single folder entry in the folder hierarchy.
/// </summary>
public sealed record FolderEntry
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentId { get; set; }
}
