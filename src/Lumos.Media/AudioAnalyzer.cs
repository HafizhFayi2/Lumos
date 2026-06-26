using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;

namespace Lumos.Media;

public class AudioAnalyzer
{
    /// <summary>
    /// Analyzes an audio file and returns a list of silent frame ranges.
    /// A "frame" here aligns with the timeline's FPS.
    /// </summary>
    public static List<(int StartFrame, int EndFrame)> DetectSilences(
        string filePath, 
        double thresholdDb = -40.0, 
        int minDurationFrames = 15, 
        double fps = 30.0)
    {
        var silences = new List<(int, int)>();

        if (!File.Exists(filePath)) return silences;

        try
        {
            using var reader = new AudioFileReader(filePath);
            int bytesPerSample = reader.WaveFormat.BitsPerSample / 8;
            int samplesPerFrame = (int)(reader.WaveFormat.SampleRate / fps);
            int bufferSize = samplesPerFrame * reader.WaveFormat.Channels;
            float[] buffer = new float[bufferSize];

            int currentFrame = 0;
            int silenceStartFrame = -1;

            double thresholdLinear = Math.Pow(10, thresholdDb / 20.0);

            while (reader.Read(buffer, 0, bufferSize) > 0)
            {
                double sumSquare = 0;
                for (int i = 0; i < bufferSize; i++)
                {
                    sumSquare += buffer[i] * buffer[i];
                }
                
                double rms = Math.Sqrt(sumSquare / bufferSize);

                if (rms < thresholdLinear)
                {
                    if (silenceStartFrame == -1)
                        silenceStartFrame = currentFrame;
                }
                else
                {
                    if (silenceStartFrame != -1)
                    {
                        if (currentFrame - silenceStartFrame >= minDurationFrames)
                        {
                            silences.Add((silenceStartFrame, currentFrame - 1));
                        }
                        silenceStartFrame = -1;
                    }
                }

                currentFrame++;
            }

            // Handle silence at the end
            if (silenceStartFrame != -1 && currentFrame - silenceStartFrame >= minDurationFrames)
            {
                silences.Add((silenceStartFrame, currentFrame - 1));
            }
        }
        catch
        {
            // Fallback or ignore if the file cannot be analyzed
        }

        return silences;
    }
}
