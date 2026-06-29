using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lumos.Media;

public interface IAudioProvider
{
    Task<float[]?> GetAudioSamplesAsync(string assetPath, double startTime, double duration);
}

/// Decodes audio samples from media files via FFmpeg.
/// Replaces the previous sine-wave stub with real PCM decoding
/// so silence/transcript analysis and audio waveform preview work correctly.
public class AudioProvider : IAudioProvider, IDisposable
{
    private readonly DecodePipeline _pipeline;
    private readonly bool _ownsPipeline;

    public AudioProvider(DecodePipeline? pipeline = null)
    {
        _ownsPipeline = pipeline == null;
        _pipeline = pipeline ?? new DecodePipeline();
    }

    public async Task<float[]?> GetAudioSamplesAsync(
        string assetPath, double startTime, double duration)
    {
        return await _pipeline.DecodeAudioSamplesAsync(
            assetPath, startTime, duration, CancellationToken.None);
    }

    public void Dispose()
    {
        if (_ownsPipeline)
            _pipeline.Dispose();
    }
}
