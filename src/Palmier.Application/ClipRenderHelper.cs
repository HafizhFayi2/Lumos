using Palmier.Domain;
using System;
using System.Collections.Generic;

namespace Palmier.Application;

public static class ClipRenderHelper
{
    public const double LabelBarHeight = 16.0;
    public const double VolumeKeyframeSize = 7.0;
    public const double VolumeKeyframeHitSize = 14.0;
    public const double VolumeFadeHandleEdgeInset = 6.0;
    public const double VolumeRubberBandTopDb = 6.0;
    public const double VolumeRubberBandBottomDb = -60.0;
    public const double FadeKneeTopInset = 4.0;

    public static DomainRect GetClipBodyRect(DomainRect clipRect)
    {
        return new DomainRect(
            clipRect.X,
            clipRect.Y + LabelBarHeight,
            clipRect.Width,
            Math.Max(0.0, clipRect.Height - LabelBarHeight - 1.0)
        );
    }

    public static double GetYForDb(double db, DomainRect body)
    {
        double clamped = Math.Min(VolumeRubberBandTopDb, Math.Max(VolumeRubberBandBottomDb, db));
        double frac = (VolumeRubberBandTopDb - clamped) / (VolumeRubberBandTopDb - VolumeRubberBandBottomDb);
        return body.Y + frac * body.Height;
    }

    public static double GetDbForY(double y, DomainRect body)
    {
        if (body.Height <= 0) return 0;
        double frac = Math.Max(0.0, Math.Min(1.0, (y - body.Y) / body.Height));
        return VolumeRubberBandTopDb - frac * (VolumeRubberBandTopDb - VolumeRubberBandBottomDb);
    }

    public static double GetFadeHandleRenderX(DomainRect clipRect, int kfOffset, bool isLeft, double pixelsPerFrame)
    {
        double actual = clipRect.X + kfOffset * pixelsPerFrame;
        if (isLeft)
        {
            return Math.Max(clipRect.X + VolumeFadeHandleEdgeInset, actual);
        }
        else
        {
            return Math.Min(clipRect.X + clipRect.Width - VolumeFadeHandleEdgeInset, actual);
        }
    }

    public static double GetFadeKneeY(DomainRect body)
    {
        return body.Y + FadeKneeTopInset;
    }

    public static List<DomainPoint> GetVolumeLinePoints(Clip clip, DomainRect rect, DomainRect body, double pixelsPerFrame)
    {
        var points = new List<DomainPoint>();
        if (clip.DurationFrames <= 0 || pixelsPerFrame <= 0) return points;

        if (clip.VolumeTrack != null && clip.VolumeTrack.IsActive)
        {
            var kfs = clip.VolumeTrack.Keyframes;
            if (kfs.Count > 0)
            {
                double firstX = rect.X + kfs[0].Frame * pixelsPerFrame;
                double firstY = GetYForDb(kfs[0].Value.Value, body);
                points.Add(new DomainPoint(rect.X, firstY));
                points.Add(new DomainPoint(firstX, firstY));

                for (int i = 0; i < kfs.Count - 1; i++)
                {
                    var a = kfs[i];
                    var b = kfs[i + 1];
                    double aX = rect.X + a.Frame * pixelsPerFrame;
                    double bX = rect.X + b.Frame * pixelsPerFrame;
                    double aY = GetYForDb(a.Value.Value, body);
                    double bY = GetYForDb(b.Value.Value, body);

                    switch (a.InterpolationOut)
                    {
                        case Interpolation.Linear:
                            points.Add(new DomainPoint(bX, bY));
                            break;
                        case Interpolation.Hold:
                            points.Add(new DomainPoint(bX, aY));
                            points.Add(new DomainPoint(bX, bY));
                            break;
                        case Interpolation.Smooth:
                            int steps = 12;
                            for (int s = 1; s <= steps; s++)
                            {
                                double t = (double)s / steps;
                                double x = aX + (bX - aX) * t;
                                double db = a.Value.Value + (b.Value.Value - a.Value.Value) * Smoothstep(t);
                                points.Add(new DomainPoint(x, GetYForDb(db, body)));
                            }
                            break;
                    }
                }

                double lastY = GetYForDb(kfs[^1].Value.Value, body);
                points.Add(new DomainPoint(rect.X + rect.Width, lastY));
            }
        }
        else
        {
            double volDb = DbFromLinear(clip.Volume);
            double volY = GetYForDb(volDb, body);
            points.Add(new DomainPoint(rect.X, volY));
            points.Add(new DomainPoint(rect.X + rect.Width, volY));
        }

        return points;
    }

    public static List<DomainPoint> GetFadeCurvePoints(DomainPoint start, DomainPoint end, Interpolation interpolation)
    {
        var points = new List<DomainPoint>();
        switch (interpolation)
        {
            case Interpolation.Linear:
            case Interpolation.Hold:
                points.Add(end);
                break;
            case Interpolation.Smooth:
                int steps = 12;
                for (int s = 1; s <= steps; s++)
                {
                    double t = (double)s / steps;
                    double x = start.X + (end.X - start.X) * t;
                    double y = start.Y + (end.Y - start.Y) * Smoothstep(t);
                    points.Add(new DomainPoint(x, y));
                }
                break;
        }
        return points;
    }

    private static double Smoothstep(double t) => t * t * (3 - 2 * t);
    private static double DbFromLinear(double vol) => vol > 0 ? 20.0 * Math.Log10(vol) : -60.0;
}
