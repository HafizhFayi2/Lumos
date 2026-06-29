using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Lumos.Application;
using Lumos.Domain;
using SkiaSharp;

namespace Lumos.Infrastructure;

/// Generates clip thumbnails using SkiaSharp (replaces manual PNG writer).
public sealed class ThumbnailGenerator : IThumbnailGenerator
{
    private const int ThumbWidth = 160;
    private const int ThumbHeight = 90;
    private readonly string _cacheDir;

    public ThumbnailGenerator(string cacheDir)
    {
        _cacheDir = cacheDir;
        Directory.CreateDirectory(cacheDir);
    }

    public async Task<string> GenerateThumbnailAsync(Asset asset, TimeSpan position, CancellationToken ct = default)
    {
        string key    = $"{Path.GetFileNameWithoutExtension(asset.FilePath)}_{(long)position.TotalMilliseconds}";
        string target = Path.Combine(_cacheDir, $"{key}.png");

        if (File.Exists(target)) return target;

        if (asset.Type == ClipType.Image)
        {
            await ExtractImageThumbnailAsync(asset, target, ct);
            return target;
        }

        if (asset.Type == ClipType.Video)
        {
            await ExtractVideoThumbnailAsync(asset, target, position, ct);
            if (File.Exists(target)) return target;
            // Fall through to checkerboard if ffmpeg fails
        }

        // Fallback for audio or failed extraction
        GenerateCheckerboardThumbnail(target);
        return target;
    }

    public async Task<List<string>> GenerateWaveformAsync(Asset asset, CancellationToken ct = default)
    {
        if (asset.Type != ClipType.Audio && asset.Type != ClipType.Video)
            return new List<string>();

        // Attempt to extract audio waveform data via ffmpeg
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffmpeg",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(asset.FilePath);
            psi.ArgumentList.Add("-af");
            psi.ArgumentList.Add("astats=metadata=1:reset=1");
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("null");
            psi.ArgumentList.Add("-");

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return new List<string>();

            string stderr = await proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);

            // Parse RMS levels from ffmpeg astats output
            var samples = new List<string>();
            var lines = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains("RMS_level:", StringComparison.OrdinalIgnoreCase))
                {
                    // ffmpeg astats output: "[Parsed_astats_0 @ ...] RMS_level: -23.5dB"
                    var parts = line.Split(':', StringSplitOptions.TrimEntries);
                    if (parts.Length >= 2)
                    {
                        // Last part contains value, e.g. "-23.5dB" or "-23.5 dB"
                        string raw = parts[^1].Replace("dB", "").Trim();
                        if (float.TryParse(raw, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float db))
                        {
                            float normalized = Math.Clamp((db + 60) / 60f, 0f, 1f);
                            samples.Add(normalized.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                        }
                    }
                }
            }

            return samples.Count > 0 ? samples : new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static async Task ExtractVideoThumbnailAsync(Asset asset, string outPath, TimeSpan position, CancellationToken ct)
    {
        System.Diagnostics.Process? proc = null;
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffmpeg",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            string seconds = position.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            psi.ArgumentList.Add("-y");
            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-loglevel");
            psi.ArgumentList.Add("error");
            psi.ArgumentList.Add("-ss");
            psi.ArgumentList.Add(seconds);
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(asset.FilePath);
            psi.ArgumentList.Add("-vframes");
            psi.ArgumentList.Add("1");
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("image2pipe");
            psi.ArgumentList.Add("-pix_fmt");
            psi.ArgumentList.Add("bgra");
            psi.ArgumentList.Add("-vf");
            psi.ArgumentList.Add($"scale={ThumbWidth}:{ThumbHeight}:force_original_aspect_ratio=decrease,pad={ThumbWidth}:{ThumbHeight}:(ow-iw)/2:(oh-ih)/2:black");
            psi.ArgumentList.Add("pipe:1");

            proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return;

            using var _ = ct.Register(() =>
            {
                try { if (!proc.HasExited) proc.Kill(); }
                catch (InvalidOperationException) { }
            });

            using var ms = new MemoryStream();
            await proc.StandardOutput.BaseStream.CopyToAsync(ms, ct);
            await proc.WaitForExitAsync(ct);

            if (ms.Length > 0 && proc.ExitCode == 0)
            {
                ms.Position = 0;
                // Decode directly to a bitmap from memory
                using var bitmap = SKBitmap.Decode(ms);
                if (bitmap != null)
                {
                    using var img = SKImage.FromBitmap(bitmap);
                    using var data = img.Encode(SKEncodedImageFormat.Png, 85);
                    await File.WriteAllBytesAsync(outPath, data.ToArray(), ct);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch
        {
            // Silent fallback — checkerboard will be used
        }
    }

    private static async Task ExtractImageThumbnailAsync(Asset asset, string outPath, CancellationToken ct)
    {
        try
        {
            // Decode image, scale to thumbnail size, pad with black borders
            using var original = SKBitmap.Decode(asset.FilePath);
            if (original == null) return;

            using var surface = SKSurface.Create(new SKImageInfo(ThumbWidth, ThumbHeight));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Black);

            float scale = Math.Min((float)ThumbWidth / original.Width, (float)ThumbHeight / original.Height);
            float drawW = original.Width * scale;
            float drawH = original.Height * scale;
            float drawX = (ThumbWidth - drawW) / 2;
            float drawY = (ThumbHeight - drawH) / 2;

            using var paint = new SKPaint { FilterQuality = SKFilterQuality.Medium };
            canvas.DrawBitmap(original, new SKRect(drawX, drawY, drawX + drawW, drawY + drawH), paint);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 85);
            await File.WriteAllBytesAsync(outPath, data.ToArray(), ct);
        }
        catch
        {
            // Silent fallback — checkerboard will be used
        }
    }

    private static void GenerateCheckerboardThumbnail(string path)
    {
        const int cell = 16;
        using var surface = SKSurface.Create(new SKImageInfo(ThumbWidth, ThumbHeight));
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(80, 80, 80));

        using var lightPaint = new SKPaint { Color = new SKColor(180, 180, 180), IsAntialias = false };
        for (int y = 0; y < ThumbHeight; y += cell)
            for (int x = 0; x < ThumbWidth; x += cell)
                if (((x / cell) + (y / cell)) % 2 == 0)
                    canvas.DrawRect(x, y, cell, cell, lightPaint);

        // "NO THUMBNAIL" text overlay
        using var textPaint = new SKPaint
        {
            Color = new SKColor(120, 120, 120, 180),
            TextSize = 12,
            IsAntialias = true,
        };
        float textW = textPaint.MeasureText("NO THUMBNAIL");
        canvas.DrawText("NO THUMBNAIL", (ThumbWidth - textW) / 2, ThumbHeight / 2 + 4, textPaint);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 85);
        File.WriteAllBytes(path, data.ToArray());
    }
}
