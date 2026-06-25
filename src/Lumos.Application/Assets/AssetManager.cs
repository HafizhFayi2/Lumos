using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Palmier.Domain;

namespace Palmier.Application.Assets;

public sealed class AssetManager
{
    private readonly AssetCatalog _catalog = new();
    private readonly AssetCache _cache = new();
    private readonly AssetIndexer _indexer = new();
    private readonly ProjectAssetRegistry _registry = new();
    private readonly IThumbnailGenerator _thumbnailGenerator;

    public AssetCatalog Catalog => _catalog;
    public AssetCache Cache => _cache;
    public AssetIndexer Indexer => _indexer;
    public ProjectAssetRegistry Registry => _registry;

    public event EventHandler<AssetEventArgs>? AssetAdded;
    public event EventHandler<AssetEventArgs>? AssetRemoved;

    public AssetManager(IThumbnailGenerator thumbnailGenerator)
    {
        _thumbnailGenerator = thumbnailGenerator;
    }

    public async Task<Asset> ImportAssetAsync(Guid projectId, string filePath, CancellationToken cancellationToken = default)
    {
        if (_catalog.TryGetByPath(filePath, out var existingAsset) && existingAsset != null)
        {
            _registry.RegisterAsset(projectId, existingAsset.Id);
            return existingAsset;
        }

        var asset = await _indexer.IndexAssetAsync(filePath, cancellationToken);
        var finalAsset = _catalog.GetOrAdd(asset);
        _registry.RegisterAsset(projectId, finalAsset.Id);

        AssetAdded?.Invoke(this, new AssetEventArgs(finalAsset, projectId));
        return finalAsset;
    }

    public async Task<List<Asset>> ImportDirectoryAsync(Guid projectId, string directoryPath, bool recursive = true, CancellationToken cancellationToken = default)
    {
        var indexed = await _indexer.IndexDirectoryAsync(directoryPath, recursive, cancellationToken);
        var result = new List<Asset>();

        foreach (var asset in indexed)
        {
            var finalAsset = _catalog.GetOrAdd(asset);
            _registry.RegisterAsset(projectId, finalAsset.Id);
            result.Add(finalAsset);

            AssetAdded?.Invoke(this, new AssetEventArgs(finalAsset, projectId));
        }

        return result;
    }

    public void RemoveAsset(Guid projectId, string assetId)
    {
        _registry.UnregisterAsset(projectId, assetId);

        if (_catalog.TryGetById(assetId, out var asset) && asset != null)
        {
            _catalog.Remove(assetId);
            _cache.Evict(assetId);
            AssetRemoved?.Invoke(this, new AssetEventArgs(asset, projectId));
        }
    }

    public async Task<string> GetOrCreateThumbnailAsync(string assetId, TimeSpan position, CancellationToken cancellationToken = default)
    {
        if (!_catalog.TryGetById(assetId, out var asset) || asset == null)
        {
            throw new ArgumentException($"Asset with ID {assetId} not found in catalog.");
        }

        if (_cache.TryGetThumbnail(assetId, position, out var cachedPath) && cachedPath != null)
        {
            return cachedPath;
        }

        var path = await _thumbnailGenerator.GenerateThumbnailAsync(asset, position);
        _cache.CacheThumbnail(assetId, position, path);
        return path;
    }

    public async Task<List<string>> GetOrCreateWaveformAsync(string assetId, CancellationToken cancellationToken = default)
    {
        if (!_catalog.TryGetById(assetId, out var asset) || asset == null)
        {
            throw new ArgumentException($"Asset with ID {assetId} not found in catalog.");
        }

        if (_cache.TryGetWaveform(assetId, out var cachedWaveforms) && cachedWaveforms != null)
        {
            return cachedWaveforms;
        }

        var waveforms = await _thumbnailGenerator.GenerateWaveformAsync(asset);
        _cache.CacheWaveform(assetId, waveforms);
        return waveforms;
    }
}

public sealed class AssetEventArgs : EventArgs
{
    public Asset Asset { get; }
    public Guid ProjectId { get; }

    public AssetEventArgs(Asset asset, Guid projectId)
    {
        Asset = asset;
        ProjectId = projectId;
    }
}
