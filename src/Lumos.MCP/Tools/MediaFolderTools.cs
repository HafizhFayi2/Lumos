using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Lumos.Application;
using Lumos.Application.Assets;

namespace Lumos.MCP.Tools;

[McpServerToolType]
public sealed class MediaFolderTools
{
    private readonly AssetManager _assets;
    private readonly MediaFolderStore _folders;

    public MediaFolderTools(AssetManager assets, MediaFolderStore folders)
    {
        _assets = assets;
        _folders = folders;
    }

    [McpServerTool(Name = ToolDefinitions.ListMediaFolders)]
    [Description("List all media folders in the project.")]
    public Task<string> ListMediaFoldersAsync(CancellationToken ct = default)
    {
        var folderList = _folders.GetAll().Select(f => new
        {
            id = f.Id,
            name = f.Name,
            parentId = f.ParentId,
            assetCount = _assets.Catalog.GetAll().Count(a => a.FolderId == f.Id),
        }).ToList();

        // Also count root-level assets (no folder)
        int rootAssetCount = _assets.Catalog.GetAll().Count(a => a.FolderId == null);

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            folders = folderList,
            rootAssetCount,
        }, new JsonSerializerOptions { WriteIndented = false }));
    }

    [McpServerTool(Name = ToolDefinitions.CreateMediaFolder)]
    [Description("Create a new media folder. Args: name (string), parent_folder_id (string, optional).")]
    public Task<string> CreateMediaFolderAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!McpToolHelpers.TryGetString(args, "name", out var name) || string.IsNullOrWhiteSpace(name))
            return Task.FromResult(McpToolHelpers.Error("name (string) is required"));

        McpToolHelpers.TryGetString(args, "parent_folder_id", out var parentId);

        var id = name.ToLowerInvariant().Replace(" ", "_") + "_" + Guid.NewGuid().ToString("N")[..6];
        var folder = new MediaFolderData(id, name, parentId);

        if (!_folders.TryAdd(folder))
            return Task.FromResult(McpToolHelpers.Error($"Folder with id '{id}' already exists."));

        return Task.FromResult(McpToolHelpers.Ok(new { id, name, parentId }));
    }

    [McpServerTool(Name = ToolDefinitions.RenameMediaFolder)]
    [Description("Rename a media folder. Args: folder_id (string), name (string).")]
    public Task<string> RenameMediaFolderAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!McpToolHelpers.TryGetString(args, "folder_id", out var folderId) || string.IsNullOrWhiteSpace(folderId))
            return Task.FromResult(McpToolHelpers.Error("folder_id (string) is required"));

        if (!McpToolHelpers.TryGetString(args, "name", out var name) || string.IsNullOrWhiteSpace(name))
            return Task.FromResult(McpToolHelpers.Error("name (string) is required"));

        if (!_folders.TryGet(folderId, out var existing) || existing == null)
            return Task.FromResult(McpToolHelpers.Error($"Folder '{folderId}' not found."));

        var updated = existing with { Name = name };
        _folders.TryUpdate(folderId, updated, existing);

        return Task.FromResult(McpToolHelpers.Ok(new { id = folderId, name }));
    }

    [McpServerTool(Name = ToolDefinitions.DeleteMediaFolder)]
    [Description("Delete a media folder and all its subfolders. Assets will be moved to root. Args: folder_id (string).")]
    public Task<string> DeleteMediaFolderAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!McpToolHelpers.TryGetString(args, "folder_id", out var folderId) || string.IsNullOrWhiteSpace(folderId))
            return Task.FromResult(McpToolHelpers.Error("folder_id (string) is required"));

        if (!_folders.TryGet(folderId, out var rootFolder) || rootFolder == null)
            return Task.FromResult(McpToolHelpers.Error($"Folder '{folderId}' not found."));

        // Collect all descendant folder IDs (including the root folder itself)
        var toRemove = new HashSet<string> { folderId };
        var queue = new Queue<string>([folderId]);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var f in _folders.GetAll())
            {
                if (f.ParentId == current && toRemove.Add(f.Id))
                    queue.Enqueue(f.Id);
            }
        }

        // Remove all folders and move their assets to root
        int movedCount = 0;
        foreach (var fid in toRemove)
        {
            _folders.TryRemove(fid, out _);

            foreach (var asset in _assets.Catalog.GetAll())
            {
                if (asset.FolderId == fid)
                {
                    asset.FolderId = null;
                    movedCount++;
                }
            }
        }

        return Task.FromResult(McpToolHelpers.Ok(new
        {
            deletedFolderId = folderId,
            deletedFolderName = rootFolder.Name,
            foldersRemoved = toRemove.Count,
            assetsMovedToRoot = movedCount,
        }));
    }

    [McpServerTool(Name = ToolDefinitions.MoveMedia)]
    [Description("Move a media asset into a folder. Args: asset_id (string), folder_id (string, or omit to place at root).")]
    public Task<string> MoveMediaAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!McpToolHelpers.TryGetString(args, "asset_id", out var assetId) || string.IsNullOrWhiteSpace(assetId))
            return Task.FromResult(McpToolHelpers.Error("asset_id (string) is required"));

        if (!_assets.Catalog.TryGetById(assetId, out var asset) || asset == null)
            return Task.FromResult(McpToolHelpers.Error($"Asset '{assetId}' not found."));

        McpToolHelpers.TryGetString(args, "folder_id", out var folderId);

        if (!string.IsNullOrEmpty(folderId) && !_folders.ContainsKey(folderId))
            return Task.FromResult(McpToolHelpers.Error($"Folder '{folderId}' not found."));

        // Update the asset's folder ID in the catalog by removing and re-adding
        asset.FolderId = folderId;

        return Task.FromResult(McpToolHelpers.Ok(new { assetId, folderId = folderId ?? "(root)" }));
    }
}
