using System;
using System.Collections.Generic;

namespace Palmier.Domain;

public enum TickType
{
    Major,
    Minor
}

public readonly record struct TimelineTick(double X, int Frame, string Timecode, TickType Type, double Height);

/// Pure calculation helper for timeline ruler tick marks.
public static class TimelineRuler
{
    public static List<TimelineTick> GetTicks(
        double width,
        double scrollOffsetX,
        double pixelsPerFrame,
        int fps)
    {
        var ticks = new List<TimelineTick>();

        if (pixelsPerFrame <= 0 || !double.IsFinite(pixelsPerFrame)) return ticks;

        int framesPerMajor = GetTickInterval(pixelsPerFrame, fps);
        if (framesPerMajor <= 0) return ticks;

        int startFrame = Math.Max(0, (int)(scrollOffsetX / pixelsPerFrame) - framesPerMajor);
        int endFrame = (int)((scrollOffsetX + width) / pixelsPerFrame) + framesPerMajor;

        int minorCount = GetMinorSubdivisions(framesPerMajor, pixelsPerFrame, fps);
        int framesPerMinor = minorCount > 0 ? framesPerMajor / minorCount : 0;

        if (framesPerMinor > 0)
        {
            int minorFrame = (startFrame / framesPerMinor) * framesPerMinor;
            while (minorFrame <= endFrame)
            {
                if (minorFrame % framesPerMajor != 0)
                {
                    double localX = minorFrame * pixelsPerFrame - scrollOffsetX;
                    if (localX >= 0 && localX <= width)
                    {
                        bool isMidpoint = minorCount % 2 == 0 && minorFrame % (framesPerMajor / 2) == 0;
                        double tickHeight = isMidpoint ? 6 : 4;
                        ticks.Add(new TimelineTick(localX, minorFrame, string.Empty, TickType.Minor, tickHeight));
                    }
                }
                minorFrame += framesPerMinor;
            }
        }

        int frame = (startFrame / framesPerMajor) * framesPerMajor;
        while (frame <= endFrame)
        {
            double localX = frame * pixelsPerFrame - scrollOffsetX;
            if (localX >= 0 && localX <= width)
            {
                string label = FormatTimecode(frame, fps);
                ticks.Add(new TimelineTick(localX, frame, label, TickType.Major, 8));
            }
            frame += framesPerMajor;
        }

        return ticks;
    }

    private static int GetTickInterval(double pixelsPerFrame, int fps)
    {
        double targetPixels = 80.0;
        double rawFrames = targetPixels / pixelsPerFrame;

        int[] candidates = { 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 1200, 1800, 3600 };
        foreach (int c in candidates)
        {
            int frames = c * fps;
            if (frames >= rawFrames) return frames;
        }
        return candidates[^1] * fps;
    }

    private static int GetMinorSubdivisions(int framesPerMajor, double pixelsPerFrame, int fps)
    {
        double majorPixels = framesPerMajor * pixelsPerFrame;
        int[] subdivisions = { 10, 5, 4, 2 };
        foreach (int divisions in subdivisions)
        {
            if (majorPixels / divisions >= 12)
            {
                return divisions;
            }
        }
        return 0;
    }

    public static string FormatTimecode(int frame, int fps)
    {
        int totalSeconds = frame / fps;
        int remainingFrames = frame % fps;
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;

        if (hours > 0)
        {
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}:{remainingFrames:D2}";
        }
        else
        {
            return $"{minutes:D2}:{seconds:D2}:{remainingFrames:D2}";
        }
    }
}
