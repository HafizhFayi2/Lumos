using Lumos.Domain;
using NAudio.Wave;

namespace Lumos.Media;

/// Applies per-frame volume automation from a clip's VolumeTrack keyframes and
/// fade envelopes to an audio sample stream. Wraps any ISampleProvider and
/// adjusts gain per frame-block (all samples sharing the same timeline frame).
///
/// The provider assumes the wrapped source produces content starting at the
/// clip's timeline position (i.e. after DelayBy / SkipOver have been applied).
/// Volume at a given frame is computed as:
///     effectiveVolume = trackVolume * clip.VolumeAt(timelineFrame)
///
/// Performance: processes per frame-block (~1470 samples at 44.1kHz/30fps)
/// instead of per-sample, reducing Clip.VolumeAt() calls by ~1470x.
public sealed class VolumeKeyframeProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly Clip _clip;
    private readonly double _trackVolume;
    private readonly double _fps;
    private readonly int _channels;
    private readonly double _samplesPerFrame; // per-channel samples in one timeline frame
    private long _totalSamplesRead; // total float samples read across all channels

    public VolumeKeyframeProvider(ISampleProvider source, Clip clip, int fps, double trackVolume = 1.0)
    {
        _source = source;
        _clip = clip;
        _trackVolume = trackVolume;
        _fps = fps;
        _channels = source.WaveFormat.Channels;
        _samplesPerFrame = source.WaveFormat.SampleRate / (double)fps;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        if (samplesRead <= 0) return samplesRead;

        int index = 0;
        while (index < samplesRead)
        {
            // Compute the timeline frame for this sample position
            long absSample = _totalSamplesRead + index;
            double timeSeconds = absSample / (double)(WaveFormat.SampleRate * _channels);
            int relativeFrame = (int)(timeSeconds * _fps);
            int timelineFrame = _clip.StartFrame + relativeFrame;

            // Get the effective volume for this frame (constant within frame)
            double clipVolume = _clip.VolumeAt(timelineFrame);
            float effectiveVolume = (float)(clipVolume * _trackVolume);

            // Compute how many samples remain in this frame block
            double nextFrameSample = (relativeFrame + 1) * _samplesPerFrame * _channels;
            long blockEndSample = Math.Min(absSample + (samplesRead - index), (long)Math.Ceiling(nextFrameSample));
            int maxBlock = samplesRead - index;
            int blockSize = Math.Max(1, Math.Min((int)(blockEndSample - absSample), maxBlock));

            // Apply the volume to this block (skip multiplication for near-unity gain)
            int blockEnd = index + blockSize;
            if (Math.Abs(effectiveVolume - 1.0f) >= 0.001f)
            {
                for (int i = index; i < blockEnd; i++)
                    buffer[offset + i] *= effectiveVolume;
            }
            index = blockEnd;
        }

        _totalSamplesRead += samplesRead;
        return samplesRead;
    }
}
