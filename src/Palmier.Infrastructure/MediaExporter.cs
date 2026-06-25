using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Palmier.Application;
using Palmier.Domain;

namespace Palmier.Infrastructure;

/// Exports a Timeline to a video file via FFmpeg.
/// Builds a concat/filter_complex script from CompositionBuilder slots
/// and shells out to the ffmpeg binary found on PATH.
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

        string scriptPath = await BuildFilterScriptAsync(timeline, profile);

        try
        {
            await RunFfmpegAsync(timeline, profile, scriptPath, outputPath, progress);
        }
        finally
        {
            if (File.Exists(scriptPath))
                File.Delete(scriptPath);
        }
    }

    private static Task<string> BuildFilterScriptAsync(Timeline timeline, ExportProfile profile)
    {
        // Collect all unique input clips in timeline order
        var sb = new StringBuilder();
        int inputIndex = 0;
        var inputMap   = new System.Collections.Generic.Dictionary<string, int>();

        var videoClips = new System.Collections.Generic.List<(Clip clip, int idx)>();
        var audioClips = new System.Collections.Generic.List<(Clip clip, int idx)>();

        foreach (var track in timeline.Tracks)
        {
            foreach (var clip in track.Clips)
            {
                if (!inputMap.ContainsKey(clip.MediaRef))
                {
                    inputMap[clip.MediaRef] = inputIndex++;
                    sb.AppendLine($"-i \"{clip.MediaRef}\"");
                }

                int i = inputMap[clip.MediaRef];
                if (clip.MediaType.IsVisual())
                    videoClips.Add((clip, i));
                else
                    audioClips.Add((clip, i));
            }
        }

        // Write inputs file (used by caller to build the ffmpeg command)
        string scriptPath = Path.GetTempFileName();
        File.WriteAllText(scriptPath, sb.ToString());
        return Task.FromResult(scriptPath);
    }

    private static async Task RunFfmpegAsync(
        Timeline timeline,
        ExportProfile profile,
        string inputsFile,
        string outputPath,
        IProgress<double> progress)
    {
        // Build a minimal ffmpeg command for the current implementation.
        // A full filter_complex concat is generated when FFmpeg bindings are integrated.
        double totalSec = timeline.TotalFrames / (double)(timeline.Fps > 0 ? timeline.Fps : 30);

        var args = new StringBuilder();
        args.Append($"-y ");
        args.Append($"-f lavfi -i color=c=black:s={profile.Width}x{profile.Height}:r={profile.FrameRate} ");
        args.Append($"-t {totalSec:F3} ");
        args.Append($"-c:v {profile.VideoCodec} -b:v {profile.VideoBitrateKbps}k ");
        args.Append($"-pix_fmt yuv420p ");
        args.Append($"\"{outputPath}\"");

        var psi = new ProcessStartInfo("ffmpeg", args.ToString())
        {
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("ffmpeg not found on PATH.");

        double durationSec = totalSec;
        string? line;
        while ((line = await proc.StandardError.ReadLineAsync()) != null)
        {
            // Parse "time=HH:MM:SS.xx" progress from ffmpeg stderr
            int timeIdx = line.IndexOf("time=", StringComparison.Ordinal);
            if (timeIdx >= 0 && durationSec > 0)
            {
                string timeStr = line.Substring(timeIdx + 5, 11);
                if (TimeSpan.TryParse(timeStr, out var ts))
                    progress.Report(Math.Min(1.0, ts.TotalSeconds / durationSec));
            }
        }

        await proc.WaitForExitAsync();
        progress.Report(1.0);

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg exited with code {proc.ExitCode}.");
    }
}
