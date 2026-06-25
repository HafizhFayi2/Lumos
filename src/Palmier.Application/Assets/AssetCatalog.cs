using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Palmier.Domain;

namespace Palmier.Application.Assets;

public sealed class AssetCatalog
{
    private readonly ConcurrentDictionary<string, Asset> _assetsById = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Asset> _assetsByPath = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGetById(string id, out Asset? asset) => _assetsById.TryGetValue(id, out asset);

    public bool TryGetByPath(string path, out Asset? asset) => _assetsByPath.TryGetValue(path, out asset);

    public Asset GetOrAdd(Asset asset)
    {
        if (_assetsByPath.TryGetValue(asset.FilePath, out var existing))
        {
            return existing;
        }

        _assetsById[asset.Id] = asset;
        _assetsByPath[asset.FilePath] = asset;
        return asset;
    }

    public bool Remove(string id)
    {
        if (_assetsById.TryRemove(id, out var asset))
        {
            _assetsByPath.TryRemove(asset.FilePath, out _);
            return true;
        }
        return false;
    }

    public IReadOnlyCollection<Asset> GetAll() => (IReadOnlyCollection<Asset>)_assetsById.Values;

    public void Clear()
    {
        _assetsById.Clear();
        _assetsByPath.Clear();
    }
}
