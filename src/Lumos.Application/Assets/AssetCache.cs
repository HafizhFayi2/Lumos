using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Lumos.Application.Assets;

public sealed class AssetCache
{
    private readonly ConcurrentDictionary<(string AssetId, long Ticks), string> _thumbnailCache = new();
    private readonly ConcurrentDictionary<string, List<string>> _waveformCache = new();

    public bool TryGetThumbnail(string assetId, TimeSpan position, out string? path)
    {
        return _thumbnailCache.TryGetValue((assetId, position.Ticks), out path);
    }

    public void CacheThumbnail(string assetId, TimeSpan position, string path)
    {
        _thumbnailCache[(assetId, position.Ticks)] = path;
    }

    public bool TryGetWaveform(string assetId, out List<string>? waveforms)
    {
        return _waveformCache.TryGetValue(assetId, out waveforms);
    }

    public void CacheWaveform(string assetId, List<string> waveforms)
    {
        _waveformCache[assetId] = waveforms;
    }

    public void Evict(string assetId)
    {
        var keysToRemove = _thumbnailCache.Keys.Where(k => k.AssetId == assetId).ToList();
        foreach (var key in keysToRemove)
        {
            _thumbnailCache.TryRemove(key, out _);
        }
        _waveformCache.TryRemove(assetId, out _);
    }

    public void Clear()
    {
        _thumbnailCache.Clear();
        _waveformCache.Clear();
    }
}
