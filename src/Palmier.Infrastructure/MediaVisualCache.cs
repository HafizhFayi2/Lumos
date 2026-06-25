using Palmier.Domain;
using Palmier.Application;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Palmier.Infrastructure;

public class MediaVisualCache
{
    private readonly ConcurrentDictionary<string, List<float>> _waveformSamples = new();
    private readonly ConcurrentDictionary<string, List<(double Time, string FilePath)>> _videoThumbnails = new();
    private readonly ConcurrentDictionary<string, string> _imageThumbnails = new();

    private readonly ConcurrentHashSet<string> _waveformInFlight = new();
    private readonly ConcurrentHashSet<string> _videoThumbnailInFlight = new();
    private readonly ConcurrentHashSet<string> _imageThumbnailInFlight = new();

    public event Action? CacheUpdated;

    public List<float>? GetSamples(string mediaRef)
    {
        return _waveformSamples.TryGetValue(mediaRef, out var samples) ? samples : null;
    }

    public List<(double Time, string FilePath)>? GetThumbnails(string mediaRef)
    {
        return _videoThumbnails.TryGetValue(mediaRef, out var thumbs) ? thumbs : null;
    }

    public string? GetImageThumbnail(string mediaRef)
    {
        return _imageThumbnails.TryGetValue(mediaRef, out var path) ? path : null;
    }

    public async Task RequestWaveformAsync(Asset asset, IThumbnailGenerator generator)
    {
        if (asset.Type != ClipType.Audio && asset.Type != ClipType.Video) return;
        
        string key = asset.Id;
        if (_waveformSamples.ContainsKey(key) || !_waveformInFlight.Add(key)) return;

        try
        {
            var samples = await generator.GenerateWaveformAsync(asset);
            var parsedSamples = new List<float>();
            foreach (var s in samples)
            {
                if (float.TryParse(s, out float val))
                {
                    parsedSamples.Add(val);
                }
            }
            _waveformSamples[key] = parsedSamples;
            CacheUpdated?.Invoke();
        }
        catch
        {
            // Ignore errors
        }
        finally
        {
            _waveformInFlight.Remove(key);
        }
    }

    public async Task RequestVideoThumbnailsAsync(Asset asset, IThumbnailGenerator generator)
    {
        if (asset.Type != ClipType.Video) return;

        string key = asset.Id;
        if (_videoThumbnails.ContainsKey(key) || !_videoThumbnailInFlight.Add(key)) return;

        try
        {
            var duration = asset.Duration;
            var interval = duration < 10 ? 1.0 : 2.0;
            var results = new List<(double Time, string FilePath)>();

            for (double time = 0; time < duration; time += interval)
            {
                var timeSpan = TimeSpan.FromSeconds(time);
                var path = await generator.GenerateThumbnailAsync(asset, timeSpan);
                if (!string.IsNullOrEmpty(path))
                {
                    results.Add((time, path));
                }
            }

            _videoThumbnails[key] = results;
            CacheUpdated?.Invoke();
        }
        catch
        {
            // Ignore
        }
        finally
        {
            _videoThumbnailInFlight.Remove(key);
        }
    }

    public async Task RequestImageThumbnailAsync(Asset asset, IThumbnailGenerator generator)
    {
        string key = asset.Id;
        if (_imageThumbnails.ContainsKey(key) || !_imageThumbnailInFlight.Add(key)) return;

        try
        {
            var path = await generator.GenerateThumbnailAsync(asset, TimeSpan.Zero);
            if (!string.IsNullOrEmpty(path))
            {
                _imageThumbnails[key] = path;
                CacheUpdated?.Invoke();
            }
        }
        catch
        {
            // Ignore
        }
        finally
        {
            _imageThumbnailInFlight.Remove(key);
        }
    }
}

internal class ConcurrentHashSet<T> where T : notnull
{
    private readonly ConcurrentDictionary<T, byte> _dictionary = new();

    public bool Add(T item) => _dictionary.TryAdd(item, 0);
    public bool Remove(T item) => _dictionary.TryRemove(item, out _);
    public bool Contains(T item) => _dictionary.ContainsKey(item);
}
