using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Lumos.Domain;

namespace Lumos.Application;

/// <summary>
/// Tracks recently opened projects for quick access.
/// Persists to a JSON file in the user's app data directory.
/// </summary>
public sealed class RecentProjectsService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _registryPath;
    private RecentProjectsData _data;

    public IReadOnlyList<RecentProjectEntry> Entries => _data.Entries;

    public RecentProjectsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var lumosDir = Path.Combine(appData, "Lumos");
        Directory.CreateDirectory(lumosDir);
        _registryPath = Path.Combine(lumosDir, ProjectDefaults.RegistryFilename);
        _data = Load();
    }

    public void RecordOpen(string projectDir, string projectName)
    {
        _data.Entries.RemoveAll(e =>
            string.Equals(e.Directory, projectDir, StringComparison.OrdinalIgnoreCase));

        _data.Entries.Insert(0, new RecentProjectEntry
        {
            Directory = projectDir,
            Name = projectName,
            LastOpened = DateTime.UtcNow,
        });

        if (_data.Entries.Count > 20)
            _data.Entries.RemoveRange(20, _data.Entries.Count - 20);

        Save();
    }

    public void Remove(string projectDir)
    {
        _data.Entries.RemoveAll(e =>
            string.Equals(e.Directory, projectDir, StringComparison.OrdinalIgnoreCase));
        Save();
    }

    private RecentProjectsData Load()
    {
        if (!File.Exists(_registryPath))
            return new RecentProjectsData();

        try
        {
            string json = File.ReadAllText(_registryPath);
            return JsonSerializer.Deserialize<RecentProjectsData>(json, JsonOpts)
                   ?? new RecentProjectsData();
        }
        catch
        {
            return new RecentProjectsData();
        }
    }

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_data, JsonOpts);
            File.WriteAllText(_registryPath, json);
        }
        catch
        {
            // Registry save failures should not crash the app.
        }
    }
}

public sealed class RecentProjectsData
{
    public List<RecentProjectEntry> Entries { get; set; } = new();
}

public sealed class RecentProjectEntry
{
    public string Directory { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime LastOpened { get; set; }
}
