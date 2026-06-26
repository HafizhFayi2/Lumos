using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Lumos.AI;

public class SemanticSearchService
{
    private readonly VectorSearchEngine _engine;
    private readonly ConcurrentDictionary<string, float[]> _embeddings = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _indexedAt = new();

    private static readonly TimeSpan RefreshThreshold = TimeSpan.FromMinutes(5);

    public SemanticSearchService(VectorSearchEngine engine)
    {
        _engine = engine;
    }

    // Index or refresh an asset. Should be called on background thread.
    public void IndexAsset(string assetPath)
    {
        try
        {
            if (_indexedAt.TryGetValue(assetPath, out var indexedAt) &&
                DateTimeOffset.UtcNow - indexedAt < RefreshThreshold)
                return;

            string description = GenerateAssetDescription(assetPath);
            float[] embedding = _engine.GetEmbedding(description);
            _embeddings[assetPath] = embedding;
            _indexedAt[assetPath] = DateTimeOffset.UtcNow;
        }
        catch
        {
            // Best-effort; missing asset or corrupt file just won't be indexed
        }
    }

    public async Task IndexAssetsAsync(IEnumerable<string> assetPaths, CancellationToken ct = default)
    {
        foreach (var path in assetPaths)
        {
            if (ct.IsCancellationRequested) break;
            await Task.Run(() => IndexAsset(path), ct);
        }
    }

    public List<(string AssetPath, float Score)> Search(string query, int topK = 10)
    {
        if (_embeddings.IsEmpty) return new List<(string, float)>();
        var snapshot = _embeddings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        return _engine.Search(query, snapshot, topK);
    }

    private static string GenerateAssetDescription(string assetPath)
    {
        // For video/image files: extract a representative frame and use the filename + extension
        // In a real SigLIP 2 implementation, you would encode the actual thumbnail image.
        // Here we use a text-based description derived from the filename and format.
        string ext = Path.GetExtension(assetPath).ToLowerInvariant();
        string name = Path.GetFileNameWithoutExtension(assetPath);
        string category = ext switch
        {
            ".mp4" or ".mov" or ".avi" or ".mkv" => "video",
            ".mp3" or ".wav" or ".aac" or ".m4a" => "audio",
            ".jpg" or ".jpeg" or ".png" or ".webp" => "image",
            _ => "media"
        };

        // Clean up the filename to make it a better query
        string cleanName = name.Replace("_", " ").Replace("-", " ");
        return $"{category} clip: {cleanName}";
    }

    public bool IsIndexed(string assetPath) => _embeddings.ContainsKey(assetPath);

    public int IndexedCount => _embeddings.Count;
}
