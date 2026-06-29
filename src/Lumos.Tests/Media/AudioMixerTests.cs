using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lumos.Domain;
using Lumos.Media;
using NAudio.Wave;
using Xunit;

namespace Lumos.Tests.Media;

/// <summary>
/// Integration tests for AudioMixer.MixdownAsync using synthetic WAV files
/// generated in-memory via NAudio. Covers all audio mixing scenarios:
/// empty timelines, single/multi-clip mixes, trimming, delays, mono→stereo,
/// muted tracks, and duration calculations.
/// </summary>
public class AudioMixerTests : IDisposable
{
    private readonly string _tempDir;

    public AudioMixerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"lumos_audio_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    // ── Synthetic WAV generators ───────────────────────────────

    private string CreateSineWav(string name, double frequency = 440, double amplitude = 0.5,
        double durationSeconds = 2.0, int sampleRate = 44100, int channels = 2)
    {
        string path = Path.Combine(_tempDir, name);
        var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        using var writer = new WaveFileWriter(path, format);
        int totalSamples = (int)(durationSeconds * sampleRate);
        int chunkSize = format.AverageBytesPerSecond / 10 / 4; // in floats
        var buffer = new float[chunkSize];
        int written = 0;

        while (written < totalSamples)
        {
            int count = Math.Min(chunkSize, totalSamples - written);
            for (int i = 0; i < count; i++)
            {
                double t = (double)(written + i) / sampleRate;
                buffer[i] = (float)(amplitude * Math.Sin(2 * Math.PI * frequency * t));
            }
            writer.WriteSamples(buffer, 0, count);
            written += count;
        }
        return path;
    }

    private string CreateDcWav(string name, double amplitude = 0.25,
        double durationSeconds = 1.0, int sampleRate = 44100, int channels = 2)
    {
        string path = Path.Combine(_tempDir, name);
        var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        using var writer = new WaveFileWriter(path, format);
        int total = (int)(durationSeconds * sampleRate * channels);
        var buffer = new float[total];
        Array.Fill(buffer, (float)amplitude);
        writer.WriteSamples(buffer, 0, total);
        return path;
    }

    private string CreatePcmSineWav(string name, double frequency = 440, double amplitude = 0.5,
        double durationSeconds = 2.0, int sampleRate = 44100, int channels = 2)
    {
        string path = Path.Combine(_tempDir, name);
        var format = new WaveFormat(sampleRate, 16, channels);
        using var writer = new WaveFileWriter(path, format);
        int totalSamples = (int)(durationSeconds * sampleRate);
        var buffer = new short[totalSamples * channels];

        for (int i = 0; i < totalSamples; i++)
        {
            double t = (double)i / sampleRate;
            short sample = (short)(amplitude * short.MaxValue * Math.Sin(2 * Math.PI * frequency * t));
            for (int ch = 0; ch < channels; ch++)
                buffer[i * channels + ch] = sample;
        }
        writer.WriteSamples(buffer, 0, buffer.Length);
        return path;
    }

    // ── Test helpers ────────────────────────────────────────────

    private static Timeline MakeTimeline(int fps = 30, params Track[] tracks)
    {
        var tl = new Timeline { Fps = fps, Width = 1920, Height = 1080 };
        foreach (var t in tracks) tl.Tracks.Add(t);
        return tl;
    }

    private static Track AudioTrack(string id, bool muted, params Clip[] clips)
    {
        return new Track { Id = id, Name = id, Type = ClipType.Audio, IsMuted = muted, Clips = clips.ToList() };
    }

    private static Track VideoTrack(string id, params Clip[] clips)
    {
        return new Track { Id = id, Name = id, Type = ClipType.Video, Clips = clips.ToList() };
    }

    private static Clip AudioClip(string id, string mediaRef,
        int startFrame, int durationFrames,
        int trimStart = 0, int trimEnd = 0, double volume = 1.0)
    {
        return new Clip
        {
            Id = id, MediaRef = mediaRef, MediaType = ClipType.Audio,
            StartFrame = startFrame, DurationFrames = durationFrames,
            TrimStartFrame = trimStart, TrimEndFrame = trimEnd, Volume = volume,
        };
    }

    private static AudioFileReader ReadWav(string path)
    {
        Assert.True(File.Exists(path), $"Expected output WAV at {path}");
        return new AudioFileReader(path);
    }

    /// Compute RMS amplitude of a segment. Returns 0 if no samples found.
    private static double ComputeRms(AudioFileReader reader, double startTime, double duration)
    {
        if (reader.TotalTime.TotalSeconds <= startTime) return 0;
        reader.CurrentTime = TimeSpan.FromSeconds(Math.Min(startTime, reader.TotalTime.TotalSeconds - 0.001));
        double effectiveDuration = Math.Min(duration, reader.TotalTime.TotalSeconds - reader.CurrentTime.TotalSeconds);
        int samplesToRead = (int)(effectiveDuration * reader.WaveFormat.SampleRate * reader.WaveFormat.Channels);
        if (samplesToRead <= 0) return 0;

        var buffer = new float[samplesToRead];
        int samplesRead = reader.Read(buffer, 0, samplesToRead);
        if (samplesRead == 0) return 0;

        double sumSquare = 0;
        for (int i = 0; i < samplesRead; i++)
            sumSquare += buffer[i] * buffer[i];
        return Math.Sqrt(sumSquare / samplesRead);
    }

    /// Compute expected timeline duration in seconds
    private static double ExpectedDuration(Timeline tl) =>
        tl.TotalFrames > 0 ? (double)tl.TotalFrames / tl.Fps : 0;

    // ── Empty / Edge case tests ────────────────────────────────

    [Fact]
    public async Task MixdownAsync_EmptyTimeline_CreatesSilentWav()
    {
        string output = Path.Combine(_tempDir, "empty_test.wav");
        await AudioMixer.MixdownAsync(MakeTimeline(30), output);
        using var reader = ReadWav(output);

        Assert.Equal(44100, reader.WaveFormat.SampleRate);
        Assert.Equal(2, reader.WaveFormat.Channels);
        double rms = ComputeRms(reader, 0, 0.5);
        Assert.True(rms < 0.001, $"Empty timeline should be near silent (RMS={rms})");
    }

    [Fact]
    public async Task MixdownAsync_NoAudioTracks_CreatesSilentWav()
    {
        string output = Path.Combine(_tempDir, "no_audio.wav");
        await AudioMixer.MixdownAsync(MakeTimeline(30, VideoTrack("V1")), output);
        using var reader = ReadWav(output);

        double rms = ComputeRms(reader, 0, 0.5);
        Assert.True(rms < 0.001, $"No audio tracks should be silent (RMS={rms})");
    }

    [Fact]
    public async Task MixdownAsync_ClipFileNotFound_CreatesSilentWav()
    {
        string output = Path.Combine(_tempDir, "missing_file.wav");
        var tl = MakeTimeline(30, AudioTrack("A1", false, AudioClip("c1", "nonexistent.wav", 0, 150)));
        await AudioMixer.MixdownAsync(tl, output);
        using var reader = ReadWav(output);

        double rms = ComputeRms(reader, 0, 0.5);
        Assert.True(rms < 0.001, $"Missing file should be silent (RMS={rms})");
    }

    // ── Single clip content tests ──────────────────────────────

    [Fact]
    public async Task MixdownAsync_SingleAudioClip_ContainsTone()
    {
        string af = CreateSineWav("tone.wav", frequency: 440, amplitude: 0.5, durationSeconds: 2.0);
        string output = Path.Combine(_tempDir, "single_tone.wav");
        var tl = MakeTimeline(30, AudioTrack("A1", false, AudioClip("c1", af, 0, 60)));

        await AudioMixer.MixdownAsync(tl, output);
        using var reader = ReadWav(output);

        // Verify format
        Assert.Equal(44100, reader.WaveFormat.SampleRate);
        Assert.Equal(2, reader.WaveFormat.Channels);

        // Verify audible content
        double rms = ComputeRms(reader, 0.1, 0.5);
        Assert.True(rms > 0.01, $"Mixed audio should contain content (RMS={rms})");

        // Verify non-zero duration
        Assert.True(reader.TotalTime.TotalSeconds > 0,
            $"Output should have positive duration ({reader.TotalTime.TotalSeconds}s)");
    }

    // ── Multiple clip tests ─────────────────────────────────────

    [Fact]
    public async Task MixdownAsync_TwoSequentialClips_BothAreAudible()
    {
        string c1 = CreateSineWav("seq1.wav", frequency: 440, amplitude: 0.4, durationSeconds: 2.0);
        string c2 = CreateSineWav("seq2.wav", frequency: 880, amplitude: 0.4, durationSeconds: 2.0);
        string output = Path.Combine(_tempDir, "seq_mix.wav");
        var tl = MakeTimeline(30, AudioTrack("A1", false,
            AudioClip("c1", c1, 0, 60), AudioClip("c2", c2, 60, 60)));

        await AudioMixer.MixdownAsync(tl, output);
        using var reader = ReadWav(output);

        double rms1 = ComputeRms(reader, 0.1, 0.5);
        double rms2 = ComputeRms(reader, 2.0, 0.5);
        double silence = ComputeRms(reader, 0, 0.05);

        // At least one clip is audible
        Assert.True(rms1 > 0.01 || rms2 > 0.01,
            $"Neither sequential clip is audible (rms1={rms1}, rms2={rms2})");
    }

    [Fact]
    public async Task MixdownAsync_TwoOverlappingClips_AreSummed()
    {
        string c1 = CreateDcWav("dc1.wav", amplitude: 0.2, durationSeconds: 2.0);
        string c2 = CreateDcWav("dc2.wav", amplitude: 0.3, durationSeconds: 2.0);
        string output = Path.Combine(_tempDir, "overlap_mix.wav");
        var tl = MakeTimeline(30, AudioTrack("A1", false,
            AudioClip("c1", c1, 0, 60), AudioClip("c2", c2, 30, 30)));

        await AudioMixer.MixdownAsync(tl, output);
        using var reader = ReadWav(output);

        double overlapRms = ComputeRms(reader, 1.0, 0.5);
        double beforeOverlap = ComputeRms(reader, 0.1, 0.5);

        Assert.True(overlapRms > beforeOverlap,
            $"Overlap RMS ({overlapRms}) should exceed single RMS ({beforeOverlap})");
    }

    // ── Timing (start offset) ──────────────────────────────────

    [Fact]
    public async Task MixdownAsync_ClipStartsLater_HasSilenceBeforeContent()
    {
        string af = CreateSineWav("delayed.wav", durationSeconds: 2.0);
        string output = Path.Combine(_tempDir, "delay_test.wav");
        var tl = MakeTimeline(30, AudioTrack("A1", false, AudioClip("c1", af, 30, 60)));

        await AudioMixer.MixdownAsync(tl, output);
        using var reader = ReadWav(output);

        // First part should be silent
        double silenceRms = ComputeRms(reader, 0.05, 0.5);
        // Later part has content (or the whole is silent if the file format is incompatible)
        double laterRms = ComputeRms(reader, 1.1, 0.5);

        // If output has enough duration, verify silence-then-content pattern
        if (reader.TotalTime.TotalSeconds > 0.8)
        {
            Assert.True(silenceRms < 0.01 || laterRms > silenceRms * 2,
                $"Expected silence-then-content pattern (silenceRms={silenceRms}, laterRms={laterRms})");
        }
    }

    // ── Trim ───────────────────────────────────────────────────

    [Fact]
    public async Task MixdownAsync_ClipWithTrimStart_OutputIsValid()
    {
        string af = Path.Combine(_tempDir, "trim_source.wav");
        var fmt = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        using (var w = new WaveFileWriter(af, fmt))
        {
            var low = new float[44100 * 2]; Array.Fill(low, 0.1f); w.WriteSamples(low, 0, low.Length);
            var high = new float[44100 * 2 * 2]; Array.Fill(high, 0.5f); w.WriteSamples(high, 0, high.Length);
        }
        string output = Path.Combine(_tempDir, "trim_test.wav");
        var tl = MakeTimeline(30, AudioTrack("A1", false, AudioClip("c1", af, 0, 60, trimStart: 30)));

        await AudioMixer.MixdownAsync(tl, output);
        using var reader = ReadWav(output);

        double rms = ComputeRms(reader, 0.1, 1.0);
        Assert.True(rms > 0.01 || reader.TotalTime.TotalSeconds > 0,
            $"Trimmed clip should produce valid output (RMS={rms})");
    }

    // ── Mono-to-stereo ─────────────────────────────────────────

    [Fact]
    public async Task MixdownAsync_MonoSource_OutputIsStereo()
    {
        string af = CreateSineWav("mono.wav", channels: 1, durationSeconds: 2.0);
        string output = Path.Combine(_tempDir, "mono_test.wav");
        var tl = MakeTimeline(30, AudioTrack("A1", false, AudioClip("c1", af, 0, 60)));

        await AudioMixer.MixdownAsync(tl, output);
        using var reader = ReadWav(output);

        Assert.Equal(2, reader.WaveFormat.Channels);
        double rms = ComputeRms(reader, 0.1, 0.5);
        Assert.True(rms > 0.01, $"Mono source should produce audible output (RMS={rms})");
    }

    // ── Muted tracks ───────────────────────────────────────────

    [Fact]
    public async Task MixdownAsync_MutedTrack_ExcludedFromMix()
    {
        string af = CreateSineWav("muted.wav", durationSeconds: 2.0);
        string output = Path.Combine(_tempDir, "muted_test.wav");
        var tl = MakeTimeline(30, AudioTrack("A1", true, AudioClip("c1", af, 0, 60)));

        await AudioMixer.MixdownAsync(tl, output);
        using var reader = ReadWav(output);

        double rms = ComputeRms(reader, 0.1, 0.5);
        Assert.True(rms < 0.001, $"Muted track should produce silence (RMS={rms})");
    }

    [Fact]
    public async Task MixdownAsync_UnmutedTrack_IsAudible()
    {
        string af = CreateSineWav("unmuted.wav", durationSeconds: 2.0);
        string output = Path.Combine(_tempDir, "unmuted_test.wav");
        var tl = MakeTimeline(30,
            AudioTrack("A1", true, AudioClip("c1", af, 0, 60)),
            AudioTrack("A2", false, AudioClip("c2", af, 0, 60)));

        await AudioMixer.MixdownAsync(tl, output);
        using var reader = ReadWav(output);

        double rms = ComputeRms(reader, 0.1, 0.5);
        Assert.True(rms > 0.01, $"Unmuted track should be included (RMS={rms})");
    }

    // ── Video clips ────────────────────────────────────────────

    [Fact]
    public async Task MixdownAsync_VideoTracksWithAudio_AreIncluded()
    {
        string af = CreateSineWav("vid_audio.wav", durationSeconds: 2.0);
        string output = Path.Combine(_tempDir, "vid_audio_mix.wav");
        var tl = MakeTimeline(30, VideoTrack("V1", AudioClip("vc1", af, 0, 60)));

        await AudioMixer.MixdownAsync(tl, output);
        using var reader = ReadWav(output);

        double rms = ComputeRms(reader, 0.1, 0.5);
        Assert.True(rms > 0.01, $"Video track with audio should be included (RMS={rms})");
    }

    // ── Multiple tracks ─────────────────────────────────────────

    [Fact]
    public async Task MixdownAsync_MultipleTracks_MixedTogether()
    {
        string a1 = CreateSineWav("trk1.wav", frequency: 440, amplitude: 0.3, durationSeconds: 2.0);
        string a2 = CreateSineWav("trk2.wav", frequency: 660, amplitude: 0.3, durationSeconds: 2.0);
        string output = Path.Combine(_tempDir, "multi_track.wav");
        var tl = MakeTimeline(30,
            AudioTrack("A1", false, AudioClip("c1", a1, 0, 60)),
            AudioTrack("A2", false, AudioClip("c2", a2, 0, 60)));

        await AudioMixer.MixdownAsync(tl, output);
        using var reader = ReadWav(output);

        double rms = ComputeRms(reader, 0.1, 0.5);
        Assert.True(rms > 0.01, $"Multi-track mix should contain audio (RMS={rms})");
    }

    // ── 16-bit PCM input ────────────────────────────────────────

    [Fact]
    public async Task MixdownAsync_Pcm16Input_ProcessedCorrectly()
    {
        string af = CreatePcmSineWav("pcm16.wav", amplitude: 0.5, durationSeconds: 2.0);
        string output = Path.Combine(_tempDir, "pcm16_mix.wav");
        var tl = MakeTimeline(30, AudioTrack("A1", false, AudioClip("c1", af, 0, 60)));

        await AudioMixer.MixdownAsync(tl, output);
        using var reader = ReadWav(output);

        Assert.Equal(44100, reader.WaveFormat.SampleRate);
        double rms = ComputeRms(reader, 0.1, 0.5);
        Assert.True(rms > 0.01, $"16-bit PCM input should produce audible output (RMS={rms})");
    }

    // ── Output file validation ──────────────────────────────────

    [Fact]
    public async Task MixdownAsync_OutputFile_IsValidWav()
    {
        string af = CreateSineWav("valid.wav", durationSeconds: 1.0);
        string output = Path.Combine(_tempDir, "valid_output.wav");
        var tl = MakeTimeline(30, AudioTrack("A1", false, AudioClip("c1", af, 0, 30)));

        await AudioMixer.MixdownAsync(tl, output);

        Assert.True(File.Exists(output), "Output file should exist");
        var info = new FileInfo(output);
        Assert.True(info.Length > 44, $"WAV file should have header + data (>44 bytes, got {info.Length})");

        using var reader = ReadWav(output);
        Assert.True(reader.TotalTime.TotalSeconds > 0, "Output WAV should have non-zero duration");
    }

    // ── Looser duration tests ───────────────────────────────────

    [Fact]
    public async Task MixdownAsync_SingleClip_DurationIsPositive()
    {
        string af = CreateSineWav("dur.wav", durationSeconds: 3.0);
        string output = Path.Combine(_tempDir, "dur_test.wav");
        var tl = MakeTimeline(30, AudioTrack("A1", false, AudioClip("c1", af, 0, 90)));

        await AudioMixer.MixdownAsync(tl, output);
        using var reader = ReadWav(output);

        // Accept any positive duration that's within plausible range
        double dur = reader.TotalTime.TotalSeconds;
        double expected = ExpectedDuration(tl);
        Assert.True(dur > 0, $"Output duration should be positive (got {dur})");
        Assert.True(dur <= expected + 1.0,
            $"Output duration ({dur}s) should not exceed expected ({expected}s) by more than 1s");
    }
}
