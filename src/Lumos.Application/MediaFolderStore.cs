using System.Collections.Concurrent;

namespace Lumos.Application;

/// A folder entry in the media folder hierarchy.
public sealed record MediaFolderData(string Id, string Name, string? ParentId);

/// Per-project media folder store, replacing the previous static dictionary
/// in MediaFolderTools. Folders are persisted in the project package as folders.json.
public sealed class MediaFolderStore
{
    private readonly ConcurrentDictionary<string, MediaFolderData> _folders = new();

    /// Seed default folders on construction.
    public MediaFolderStore()
    {
        SeedDefaults();
    }

    private void SeedDefaults()
    {
        _folders.TryAdd("imports", new MediaFolderData("imports", "Imports", null));
        _folders.TryAdd("generated", new MediaFolderData("generated", "Generated", null));
        _folders.TryAdd("music", new MediaFolderData("music", "Music", null));
    }

    public bool TryGet(string id, out MediaFolderData? folder) => _folders.TryGetValue(id, out folder);

    public bool ContainsKey(string id) => _folders.ContainsKey(id);

    public bool TryAdd(MediaFolderData folder) => _folders.TryAdd(folder.Id, folder);

    public bool TryRemove(string id, out MediaFolderData? removed) => _folders.TryRemove(id, out removed);

    public bool TryUpdate(string id, MediaFolderData updated, MediaFolderData existing)
        => _folders.TryUpdate(id, updated, existing);

    public IReadOnlyCollection<MediaFolderData> GetAll() => (IReadOnlyCollection<MediaFolderData>)_folders.Values;

    public IEnumerable<MediaFolderData> GetChildren(string parentId)
        => _folders.Values.Where(f => f.ParentId == parentId);

    public void Clear()
    {
        _folders.Clear();
        SeedDefaults();
    }

    /// Replace all folders with a given list (used when loading from project).
    public void ReplaceAll(IEnumerable<MediaFolderData> folders)
    {
        _folders.Clear();
        foreach (var f in folders)
            _folders.TryAdd(f.Id, f);
    }
}
