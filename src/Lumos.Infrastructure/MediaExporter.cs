using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Lumos.Application;
using Lumos.Domain;
using Lumos.Media;

namespace Lumos.Infrastructure;

public sealed class MediaExporter : IMediaExporter
{
    public async Task ExportAsync(
        Timeline timeline,
        ExportProfile profile,
        string outputPath,
        IProgress<double> progress)
    {
        if (timeline.TotalFrames == 0)
            throw new InvalidOperationException("Timeline is empty.");

        int width = profile.Width > 0 ? profile.Width : timeline.Width;
        int height = profile.Height > 0 ? profile.Height : timeline.Height;
        double fps = profile.FrameRate > 0 ? profile.FrameRate : timeline.Fps;

        string tempAudioPath = Path.Combine(Path.GetTempPath(), $"lumos_export_{Guid.NewGuid()}.wav");
        await AudioMixer.MixdownAsync(timeline, tempAudioPath);

        string args = $"-y -f rawvideo -pix_fmt bgra -s {width}x{height} -r {fps} -i - -i \"{tempAudioPath}\" -c:v {profile.VideoCodec} -b:v {profile.VideoBitrateKbps}k -c:a {profile.AudioCodec} -b:a {profile.AudioBitrateKbps}k -pix_fmt yuv420p -shortest \"{outputPath}\"";

        var psi = new ProcessStartInfo("ffmpeg", args)
        {
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("ffmpeg not found on PATH.");

        // We don't read stderr to parse progress because we control the frame loop
        // We will just read it in background to avoid blocking
        _ = Task.Run(async () => {
            while (await proc.StandardError.ReadLineAsync() != null) {}
        });

        using var compositor = new SkiaCompositor(width, height);
        var builder = new CompositionBuilder();
        builder.Load(timeline);
        
        for (int i = 0; i < timeline.TotalFrames; i++)
        {
            var frame = builder.Build(i);
            byte[] pixelData = await compositor.CompositeAsync(frame);
            
            await proc.StandardInput.BaseStream.WriteAsync(pixelData, 0, pixelData.Length);
            
            if (i % 10 == 0)
            {
                progress.Report((double)i / timeline.TotalFrames);
            }
        }

        proc.StandardInput.Close();
        await proc.WaitForExitAsync();
        progress.Report(1.0);

        if (File.Exists(tempAudioPath))
            File.Delete(tempAudioPath);

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg exited with code {proc.ExitCode}.");
    }
}
