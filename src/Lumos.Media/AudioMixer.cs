using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Lumos.Domain;

namespace Lumos.Media;

public class AudioMixer
{
    public static async Task MixdownAsync(Timeline timeline, string outputPath)
    {
        await Task.Run(() =>
        {
            var audioClips = timeline.Tracks
                .SelectMany(t => t.Clips)
                .Where(c => c.MediaType == ClipType.Audio || c.MediaType == ClipType.Video)
                .OrderBy(c => c.StartFrame)
                .ToList();

            if (audioClips.Count == 0)
            {
                // Create an empty silent WAV file
                using var writer = new WaveFileWriter(outputPath, WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
                return;
            }

            var mixers = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            var providers = new List<ISampleProvider>();

            foreach (var clip in audioClips)
            {
                if (!File.Exists(clip.MediaRef)) continue;

                try
                {
                    var reader = new AudioFileReader(clip.MediaRef);
                    // Trim start
                    double skipSeconds = clip.TrimStartFrame / timeline.Fps;
                    double takeSeconds = clip.DurationFrames / timeline.Fps;
                    
                    var offsetProvider = new OffsetSampleProvider(reader)
                    {
                        SkipOver = TimeSpan.FromSeconds(skipSeconds),
                        Take = TimeSpan.FromSeconds(takeSeconds),
                        DelayBy = TimeSpan.FromSeconds(clip.StartFrame / timeline.Fps)
                    };

                    // Ensure stereo
                    ISampleProvider finalProvider = offsetProvider;
                    if (reader.WaveFormat.Channels == 1)
                    {
                        finalProvider = new MonoToStereoSampleProvider(offsetProvider);
                    }
                    else if (reader.WaveFormat.Channels > 2)
                    {
                        // Simplified multiplex down to stereo
                        finalProvider = new MultiplexingSampleProvider(
                            new[] { offsetProvider }, 2);
                    }

                    // Apply volume effect if any
                    double volume = 1.0;
                    // For now simple volume:
                    var volProvider = new VolumeSampleProvider(finalProvider) { Volume = (float)volume };
                    
                    providers.Add(volProvider);
                }
                catch
                {
                    // Ignore unsupported audio
                }
            }

            if (providers.Count > 0)
            {
                foreach (var provider in providers)
                {
                    mixers.AddMixerInput(provider);
                }
                double totalSeconds = timeline.TotalFrames / timeline.Fps;
                var finalMix = new TakeSampleProvider(mixers, TimeSpan.FromSeconds(totalSeconds));
                WaveFileWriter.CreateWaveFile16(outputPath, finalMix);
            }
            else
            {
                using var writer = new WaveFileWriter(outputPath, WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            }
        });
    }

    // Helper class for duration
    private class TakeSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly int _samplesToTake;
        private int _samplesTaken;

        public TakeSampleProvider(ISampleProvider source, TimeSpan duration)
        {
            _source = source;
            _samplesToTake = (int)(duration.TotalSeconds * source.WaveFormat.SampleRate * source.WaveFormat.Channels);
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesNeeded = Math.Min(count, _samplesToTake - _samplesTaken);
            if (samplesNeeded <= 0) return 0;
            
            int samplesRead = _source.Read(buffer, offset, samplesNeeded);
            _samplesTaken += samplesRead;
            return samplesRead;
        }
    }
}
