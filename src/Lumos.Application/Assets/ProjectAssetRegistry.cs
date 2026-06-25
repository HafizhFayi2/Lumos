using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Palmier.Application.Assets;

public sealed class ProjectAssetRegistry
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _projectAssets = new();

    public void RegisterAsset(Guid projectId, string assetId)
    {
        var assets = _projectAssets.GetOrAdd(projectId, _ => new ConcurrentDictionary<string, byte>());
        assets.TryAdd(assetId, 0);
    }

    public void UnregisterAsset(Guid projectId, string assetId)
    {
        if (_projectAssets.TryGetValue(projectId, out var assets))
        {
            assets.TryRemove(assetId, out _);
        }
    }

    public IReadOnlyCollection<string> GetProjectAssets(Guid projectId)
    {
        if (_projectAssets.TryGetValue(projectId, out var assets))
        {
            return (IReadOnlyCollection<string>)assets.Keys;
        }
        return Array.Empty<string>();
    }

    public void ClearProject(Guid projectId)
    {
        _projectAssets.TryRemove(projectId, out _);
    }

    public void Clear()
    {
        _projectAssets.Clear();
    }
}
