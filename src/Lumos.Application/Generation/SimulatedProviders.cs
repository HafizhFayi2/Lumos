using System.Diagnostics;
using System.Globalization;
using System.Text;
using SkiaSharp;

namespace Lumos.Application.Generation;

/// Simulated image provider — creates a real PNG with prompt text rendered onto
/// a gradient background using SkiaSharp. No API key required.
public sealed class SimulatedImageProvider : IModelProvider
{
    public string Name => "Simulated Image";
    public ModelProviderType Type => ModelProviderType.Image;

    public async Task<GenerationResult> GenerateAsync(
        GenerationRequest request,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        await Task.Delay(1500, ct);
        progress?.Report(0.4);

        int w = Math.Clamp(request.Width ?? 512, 64, 4096);
        int h = Math.Clamp(request.Height ?? 512, 64, 4096);

        var tempDir = Path.Combine(Path.GetTempPath(), "LumosGeneration");
        Directory.CreateDirectory(tempDir);
        var outputPath = Path.Combine(tempDir, $"img_{Guid.NewGuid():N}.png");

        progress?.Report(0.7);
        CreatePng(outputPath, w, h, request.Prompt);
        progress?.Report(1.0);

        return new GenerationResult(true, outputPath, null);
    }

    private static void CreatePng(string path, int width, int height, string prompt)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;

        // Gradient background: purple (#8B5CF6) → blue (#3B82F6)
        using var bgPaint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(width, height),
                [new SKColor(0x8B, 0x5C, 0xF6), new SKColor(0x3B, 0x82, 0xF6)],
                SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(0, 0, width, height, bgPaint);

        // Noise texture overlay
        var random = new Random();
        using var noisePaint = new SKPaint { Color = new SKColor(255, 255, 255, 12) };
        for (int i = 0; i < width * height / 64; i++)
            canvas.DrawPoint(random.Next(width), random.Next(height), noisePaint);

        // Prompt text centered on image
        float fontSize = Math.Min(width, height) / 18f;
        fontSize = Math.Clamp(fontSize, 12, 64);
        using var typeface = SKTypeface.FromFamilyName("Segoe UI");
        using var textPaint = new SKPaint
        {
            Typeface = typeface,
            TextSize = fontSize,
            Color = SKColors.White,
            IsAntialias = true,
            SubpixelText = true,
        };

        // Word-wrap the prompt
        var lines = WordWrap(prompt, textPaint, width - 40);
        float lineHeight = textPaint.TextSize * 1.35f;
        float totalH = lines.Length * lineHeight;
        float startY = (height - totalH) / 2 + lineHeight;

        // Text shadow
        using var shadowPaint = new SKPaint
        {
            Typeface = typeface,
            TextSize = fontSize,
            Color = new SKColor(0, 0, 0, 100),
            IsAntialias = true,
        };
        for (int i = 0; i < lines.Length; i++)
        {
            float x = (width - textPaint.MeasureText(lines[i])) / 2;
            canvas.DrawText(lines[i], x + 1, startY + i * lineHeight + 1, shadowPaint);
            canvas.DrawText(lines[i], x, startY + i * lineHeight, textPaint);
        }

        // "SIMULATED" badge at bottom-right
        using var badgePaint = new SKPaint
        {
            Typeface = typeface,
            TextSize = 10,
            Color = new SKColor(180, 180, 180),
            IsAntialias = true,
        };
        string badge = "SIMULATED";
        float badgeW = badgePaint.MeasureText(badge) + 16;
        float badgeH = 20;
        using var badgeBg = new SKPaint { Color = new SKColor(0, 0, 0, 140) };
        canvas.DrawRoundRect(new SKRect(width - badgeW - 8, height - badgeH - 8, width - 8, height - 8), 4, 4, badgeBg);
        canvas.DrawText(badge, width - badgeW + 8, height - badgeH - 8 + 14, badgePaint);

        // Save to file
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        using var fs = File.Create(path);
        data.SaveTo(fs);
    }

    private static string[] WordWrap(string text, SKPaint paint, float maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<string>();
        var words = text.Split(' ');
        var lines = new List<string>();
        var currentLine = new StringBuilder();

        foreach (var word in words)
        {
            var testLine = currentLine.Length == 0 ? word : currentLine + " " + word;
            float lineWidth = paint.MeasureText(testLine);
            if (lineWidth > maxWidth && currentLine.Length > 0)
            {
                lines.Add(currentLine.ToString());
                currentLine.Clear();
                currentLine.Append(word);
            }
            else
            {
                if (currentLine.Length > 0) currentLine.Append(' ');
                currentLine.Append(word);
            }
        }

        if (currentLine.Length > 0)
            lines.Add(currentLine.ToString());

        return lines.ToArray();
    }
}

/// Simulated video provider — generates a short real MP4 using ffmpeg with
/// a color bar + prompt text overlay. Requires ffmpeg on PATH.
public sealed class SimulatedVideoProvider : IModelProvider
{
    public string Name => "Simulated Video";
    public ModelProviderType Type => ModelProviderType.Video;

    public async Task<GenerationResult> GenerateAsync(
        GenerationRequest request,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        await Task.Delay(2000, ct);
        progress?.Report(0.3);

        int w = Math.Clamp(request.Width ?? 640, 64, 4096);
        int h = Math.Clamp(request.Height ?? 360, 64, 4096);
        int duration = Math.Clamp(request.DurationSeconds ?? 5, 1, 60);
        string sanitized = SanitizeForDrawtext(request.Prompt);

        var tempDir = Path.Combine(Path.GetTempPath(), "LumosGeneration");
        Directory.CreateDirectory(tempDir);
        var outputPath = Path.Combine(tempDir, $"vid_{Guid.NewGuid():N}.mp4");

        progress?.Report(0.6);

        try
        {
            await CreateMp4WithFfmpegAsync(outputPath, w, h, duration, sanitized, ct);
        }
        catch
        {
            // Fallback: write a metadata file if ffmpeg is not available
            var meta = new
            {
                type = "simulated_video",
                prompt = request.Prompt,
                generatedAt = DateTime.UtcNow,
                model = request.ModelId,
                width = w,
                height = h,
                durationSeconds = duration,
                note = "Install ffmpeg and add it to PATH for real simulated video generation.",
            };
            var json = System.Text.Json.JsonSerializer.Serialize(meta);
            File.WriteAllText(Path.ChangeExtension(outputPath, ".json"), json);
            File.WriteAllText(outputPath, $"SIMULATED_VIDEO:{request.Prompt}");
        }

        progress?.Report(1.0);
        return new GenerationResult(true, outputPath, null);
    }

    private static async Task CreateMp4WithFfmpegAsync(
        string path, int width, int height, int duration, string prompt, CancellationToken ct)
    {
        int fps = 24;
        int totalFrames = fps * duration;

        // Generate a test pattern with color bars using ffmpeg
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Color bars + timestamp + prompt text overlay
        string drawtext = $"drawtext=text='{prompt}':fontsize={Math.Max(12, width / 30)}:fontcolor=white:x=(w-text_w)/2:y=(h-text_h)/2:enable='between(t,0,{duration})'";
        string timestamp = $"drawtext=text='%{{pts\\:hms}}':fontsize={Math.Max(10, width / 50)}:fontcolor=white@0.6:x=w-text_w-10:y=10:enable='between(t,0,{duration})'";

        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("lavfi");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add($"testsrc=duration={duration}:size={width}x{height}:rate={fps}");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("lavfi");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add($"sine=frequency=440:duration={duration}:sample_rate=44100");
        psi.ArgumentList.Add("-c:v");
        psi.ArgumentList.Add("libx264");
        psi.ArgumentList.Add("-preset");
        psi.ArgumentList.Add("ultrafast");
        psi.ArgumentList.Add("-pix_fmt");
        psi.ArgumentList.Add("yuv420p");
        psi.ArgumentList.Add("-c:a");
        psi.ArgumentList.Add("aac");
        psi.ArgumentList.Add("-vf");
        psi.ArgumentList.Add($"{drawtext},{timestamp}");
        psi.ArgumentList.Add("-shortest");
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add(path);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("ffmpeg could not be started.");
        string stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(stderr)
                    ? "ffmpeg exited with non-zero code"
                    : stderr.Trim());
        }
    }

    private static string SanitizeForDrawtext(string text)
    {
        // Escape characters that are special in ffmpeg drawtext
        return text
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace(":", "\\:")
            .Replace("\"", "\\\"")
            // ! is safe inside single-quoted drawtext expressions
            .Replace("{", "\\{")
            .Replace("}", "\\}")
            .Replace("\n", " ");
    }
}

/// Simulated audio provider — generates a real WAV file with a sine wave tone
/// whose frequency varies based on the prompt text hash.
public sealed class SimulatedAudioProvider : IModelProvider
{
    public string Name => "Simulated Audio";
    public ModelProviderType Type => ModelProviderType.Audio;

    public async Task<GenerationResult> GenerateAsync(
        GenerationRequest request,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        await Task.Delay(1500, ct);
        progress?.Report(0.5);

        int duration = Math.Clamp(request.DurationSeconds ?? 5, 1, 120);
        var tempDir = Path.Combine(Path.GetTempPath(), "LumosGeneration");
        Directory.CreateDirectory(tempDir);
        var outputPath = Path.Combine(tempDir, $"aud_{Guid.NewGuid():N}.wav");

        progress?.Report(0.8);
        CreateWav(outputPath, duration, request.Prompt);
        progress?.Report(1.0);

        return new GenerationResult(true, outputPath, null);
    }

    private static void CreateWav(string path, int durationSeconds, string prompt)
    {
        int sampleRate = 44100;
        int channels = 2;
        short bitsPerSample = 16;
        int totalSamples = sampleRate * durationSeconds;
        int dataSize = totalSamples * channels * (bitsPerSample / 8);

        // Generate a tonal sequence based on the prompt hash
        int baseFreq = 220 + Math.Abs(prompt.GetHashCode()) % 440;
        double volume = 0.3;

        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs, Encoding.UTF8);

        // RIFF header
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataSize);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));

        // fmt chunk
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);               // chunk size
        bw.Write((short)1);         // PCM format
        bw.Write((short)channels);  // mono
        bw.Write(sampleRate);
        bw.Write(sampleRate * channels * (bitsPerSample / 8)); // byte rate
        bw.Write((short)(channels * (bitsPerSample / 8)));     // block align
        bw.Write(bitsPerSample);

        // data chunk
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataSize);

        // Generate audio samples
        // Create a tone that evolves: starts at baseFreq, sweeps up then down
        for (int i = 0; i < totalSamples; i++)
        {
            double t = (double)i / sampleRate;

            // Frequency sweep
            double sweepPos = t / durationSeconds; // 0..1
            double freq;
            if (sweepPos < 0.5)
                freq = baseFreq + (baseFreq * 0.5) * (sweepPos / 0.5);       // ramp up
            else
                freq = (baseFreq + baseFreq * 0.5) - (baseFreq * 0.5) * ((sweepPos - 0.5) / 0.5); // ramp down

            // Generate sine
            double sample = Math.Sin(2 * Math.PI * freq * t);

            // Add harmonics for richness
            sample += 0.3 * Math.Sin(2 * Math.PI * freq * 2 * t);  // 2nd harmonic
            sample += 0.15 * Math.Sin(2 * Math.PI * freq * 3 * t); // 3rd harmonic

            // Apply volume envelope (fade in/out)
            double envelope = 1.0;
            double fadeLen = 0.1; // 100ms fade
            if (t < fadeLen)
                envelope = t / fadeLen;
            else if (t > durationSeconds - fadeLen)
                envelope = (durationSeconds - t) / fadeLen;

            sample *= volume * envelope;

            // Clamp
            sample = Math.Clamp(sample, -1.0, 1.0);

            short val = (short)(sample * short.MaxValue);

            // Write both channels (stereo)
            bw.Write(val);
            bw.Write(val);
        }
    }
}
