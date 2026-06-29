using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime;

namespace Lumos.Media;

public interface IFrameCache
{
    void AddFrame(string assetId, int frameNumber, byte[] pixelData);
    byte[]? GetFrame(string assetId, int frameNumber);
    void Clear();
    /// <summary>
    /// Try to shrink the cache. Returns number of bytes freed.
    /// </summary>
    long Shrink(long targetBytes);
    long CurrentSizeBytes { get; }
    int Count { get; }
    /// <summary>
    /// Percentage of max cache bytes currently used (0.0 – 1.0).
    /// </summary>
    double UsageRatio { get; }
}

public class FrameCache : IFrameCache
{
    private readonly ConcurrentDictionary<(string AssetId, int Frame), FrameCacheEntry> _cache = new();
    private readonly LinkedList<(string AssetId, int Frame)> _lruList = new();
    private readonly object _lock = new();
    private readonly long _maxCacheBytes;
    private long _currentCacheBytes;

    public long CurrentSizeBytes { get { lock (_lock) { return _currentCacheBytes; } } }
    public int Count { get { lock (_lock) { return _cache.Count; } } }
    public double UsageRatio { get { lock (_lock) { return _maxCacheBytes > 0 ? (double)_currentCacheBytes / _maxCacheBytes : 0; } } }

    public FrameCache(long maxCacheBytes = 512 * 1024 * 1024)
    {
        _maxCacheBytes = maxCacheBytes;
    }

    public void AddFrame(string assetId, int frameNumber, byte[] pixelData)
    {
        var key = (assetId, frameNumber);
        long entrySize = pixelData.Length;

        // Reject oversized single frames (e.g. corrupted data)
        if (entrySize > _maxCacheBytes)
        {
            Debug.WriteLine($"[FrameCache] Frame too large ({entrySize} bytes), not caching.");
            return;
        }

        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var existing))
            {
                _currentCacheBytes -= existing.Data.Length;
                _cache[key] = new FrameCacheEntry(pixelData);
                _currentCacheBytes += entrySize;
                TryRemoveFromList(key);
                _lruList.AddFirst(key);
                return;
            }

            EvictUntilSpace(entrySize);

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
                TryRemoveFromList(key);
                _lruList.AddFirst(key);
                return entry.Data;
            }
            return null;
        }
    }

    /// <summary>
    /// Try to shrink the cache by evicting up to <paramref name="targetBytes"/>.
    /// Returns the number of bytes actually freed.
    /// </summary>
    public long Shrink(long targetBytes)
    {
        long freed = 0;
        lock (_lock)
        {
            while (freed < targetBytes && _lruList.Count > 0)
            {
                var oldestKey = _lruList.Last;
                if (oldestKey == null) break;
                _lruList.RemoveLast();
                if (_cache.TryRemove(oldestKey.Value, out var evicted))
                {
                    freed += evicted.Data.Length;
                    _currentCacheBytes -= evicted.Data.Length;
                }
            }
        }
        return freed;
    }

    /// <summary>
    /// Evict all entries. Resets the cache to empty.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _lruList.Clear();
            _currentCacheBytes = 0;
        }
    }

    private void EvictUntilSpace(long neededBytes)
    {
        Debug.Assert(Monitor.IsEntered(_lock), "EvictUntilSpace must be called under lock");

        // Under memory pressure, evict more aggressively
        long pressureMultiplier = IsUnderMemoryPressure() ? 2L : 1L;

        while (_currentCacheBytes + neededBytes * pressureMultiplier > _maxCacheBytes && _lruList.Count > 0)
        {
            var oldestKey = _lruList.Last;
            if (oldestKey == null) break;
            _lruList.RemoveLast();
            if (_cache.TryRemove(oldestKey.Value, out var evicted))
            {
                _currentCacheBytes -= evicted.Data.Length;
            }
        }
    }

    private void TryRemoveFromList((string AssetId, int Frame) key)
    {
        Debug.Assert(Monitor.IsEntered(_lock), "TryRemoveFromList must be called under lock");
        var node = _lruList.Find(key);
        if (node != null)
            _lruList.Remove(node);
    }

    private static bool IsUnderMemoryPressure()
    {
        try
        {
            // Check total managed memory vs physical memory
            var memInfo = GC.GetGCMemoryInfo();
            long totalCommitted = memInfo.TotalCommittedBytes;
            long highMemoryThreshold = memInfo.HighMemoryLoadThresholdBytes;
            return totalCommitted > 0 && highMemoryThreshold > 0
                && totalCommitted > highMemoryThreshold / 2;
        }
        catch
        {
            return false;
        }
    }

    private sealed record FrameCacheEntry(byte[] Data);
}
