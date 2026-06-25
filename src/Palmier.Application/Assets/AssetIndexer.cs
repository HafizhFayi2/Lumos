using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Palmier.Domain;

namespace Palmier.Application.Assets;

public sealed class AssetIndexer
{
    public async Task<Asset> IndexAssetAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Asset file not found", filePath);
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var name = Path.GetFileName(filePath);
        var fileInfo = new FileInfo(filePath);
        var length = fileInfo.Length;

        await Task.Delay(10, cancellationToken);

        var asset = new Asset
        {
            FilePath = filePath,
            Name = name,
            FolderId = null
        };

        switch (extension)
        {
            case ".mp4":
            case ".mov":
            case ".avi":
            case ".mkv":
            case ".m4v":
                asset.Type = ClipType.Video;
                asset.Duration = EstimateVideoDuration(length);
                asset.SourceWidth = 1920;
                asset.SourceHeight = 1080;
                asset.SourceFps = 30.0;
                asset.HasAudio = true;
                break;

            case ".mp3":
            case ".wav":
            case ".aac":
            case ".m4a":
            case ".flac":
            case ".ogg":
                asset.Type = ClipType.Audio;
                asset.Duration = EstimateAudioDuration(length);
                asset.HasAudio = true;
                break;

            case ".png":
            case ".jpg":
            case ".jpeg":
            case ".gif":
            case ".bmp":
            case ".tiff":
                asset.Type = ClipType.Image;
                asset.Duration = 0.0;
                asset.SourceWidth = 1920;
                asset.SourceHeight = 1080;
                asset.HasAudio = false;
                break;

            case ".srt":
            case ".vtt":
            case ".txt":
                asset.Type = ClipType.Text;
                asset.Duration = EstimateTextDuration(filePath);
                asset.HasAudio = false;
                break;

            case ".json":
                asset.Type = ClipType.Lottie;
                asset.Duration = 5.0;
                asset.SourceWidth = 1080;
                asset.SourceHeight = 1080;
                asset.SourceFps = 60.0;
                asset.HasAudio = false;
                break;

            default:
                asset.Type = ClipType.Video;
                asset.Duration = 10.0;
                break;
        }

        return asset;
    }

    public async Task<List<Asset>> IndexDirectoryAsync(string directoryPath, bool recursive = true, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
        {
            return new List<Asset>();
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mov", ".avi", ".mkv", ".m4v",
            ".mp3", ".wav", ".aac", ".m4a", ".flac", ".ogg",
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff",
            ".srt", ".vtt", ".txt", ".json"
        };

        var files = Directory.EnumerateFiles(directoryPath, "*.*", searchOption)
            .Where(f => supportedExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        var indexedAssets = new List<Asset>();
        var semaphore = new SemaphoreSlim(4);
        var tasks = files.Select(async file =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var asset = await IndexAssetAsync(file, cancellationToken);
                lock (indexedAssets)
                {
                    indexedAssets.Add(asset);
                }
            }
            catch
            {
                // Ignore indexing failures for individual files
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return indexedAssets;
    }

    private static double EstimateVideoDuration(long fileLength)
    {
        double bytesPerSecond = 5 * 1024 * 1024;
        return Math.Max(1.0, Math.Round((double)fileLength / bytesPerSecond, 2));
    }

    private static double EstimateAudioDuration(long fileLength)
    {
        double bytesPerSecond = 32 * 1024;
        return Math.Max(1.0, Math.Round((double)fileLength / bytesPerSecond, 2));
    }

    private static double EstimateTextDuration(string filePath)
    {
        try
        {
            var lines = File.ReadLines(filePath).Take(100).Count();
            return Math.Max(5.0, lines * 1.5);
        }
        catch
        {
            return 10.0;
        }
    }
}
