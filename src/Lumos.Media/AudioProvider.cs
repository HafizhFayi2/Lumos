using System;
using System.Threading.Tasks;

namespace Lumos.Media;

public interface IAudioProvider
{
    Task<float[]?> GetAudioSamplesAsync(string assetPath, double startTime, double duration);
}

public class AudioProvider : IAudioProvider
{
    public async Task<float[]?> GetAudioSamplesAsync(string assetPath, double startTime, double duration)
    {
        return await Task.Run(() =>
        {
            int sampleCount = (int)(44100 * duration);
            if (sampleCount <= 0) return null;
            
            float[] samples = new float[sampleCount];
            double frequency = 220.0;
            for (int i = 0; i < sampleCount; i++)
            {
                double t = (double)i / 44100.0;
                samples[i] = (float)(0.2 * Math.Sin(2.0 * Math.PI * frequency * t));
            }
            return samples;
        });
    }
}
