using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Palmier.Media;

public interface IFrameCache
{
    void AddFrame(string assetId, int frameNumber, byte[] pixelData);
    byte[]? GetFrame(string assetId, int frameNumber);
    void Clear();
}

public class FrameCache : IFrameCache
{
    private readonly ConcurrentDictionary<(string AssetId, int Frame), FrameCacheEntry> _cache = new();
    private readonly LinkedList<(string AssetId, int Frame)> _lruList = new();
    private readonly object _lock = new();
    private readonly long _maxCacheBytes;
    private long _currentCacheBytes;

    public FrameCache(long maxCacheBytes = 512 * 1024 * 1024)
    {
        _maxCacheBytes = maxCacheBytes;
    }

    public void AddFrame(string assetId, int frameNumber, byte[] pixelData)
    {
        var key = (assetId, frameNumber);
        long entrySize = pixelData.Length;

        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var existing))
            {
                _currentCacheBytes -= existing.Data.Length;
                _cache[key] = new FrameCacheEntry(pixelData);
                _currentCacheBytes += entrySize;
                _lruList.Remove(key);
                _lruList.AddFirst(key);
                return;
            }

            while (_currentCacheBytes + entrySize > _maxCacheBytes && _lruList.Count > 0)
            {
                var oldestKey = _lruList.Last!.Value;
                _lruList.RemoveLast();
                if (_cache.TryRemove(oldestKey, out var evicted))
                {
                    _currentCacheBytes -= evicted.Data.Length;
                }
            }

            _cache[key] = new FrameCacheEntry(pixelData);
            _currentCacheBytes += entrySize;
            _lruList.AddFirst(key);
        }
    }

    public byte[]? GetFrame(string assetId, int frameNumber)
    {
        var key = (assetId, frameNumber);
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                _lruList.Remove(key);
                _lruList.AddFirst(key);
                return entry.Data;
            }
            return null;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _lruList.Clear();
            _currentCacheBytes = 0;
        }
    }

    private sealed record FrameCacheEntry(byte[] Data);
}
