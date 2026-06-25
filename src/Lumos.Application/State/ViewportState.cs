using System;

namespace Lumos.Application.State;

/// Immutable viewport state. Tracks zoom level, scroll position, and ruler visibility.
public sealed record ViewportState
{
    public double PixelsPerFrame { get; init; } = Lumos.Domain.Defaults.PixelsPerFrame;
    public double ScrollX { get; init; }
    public double ScrollY { get; init; }
    public double ViewportWidth { get; init; } = 800;
    public double ViewportHeight { get; init; } = 300;
    public bool ShowRuler { get; init; } = true;

    public double ZoomPercent => PixelsPerFrame / Lumos.Domain.Defaults.PixelsPerFrame * 100;

    // ── Transition methods ──────────────────────────────────────────────────

    public ViewportState SetZoom(double pixelsPerFrame) =>
        this with
        {
            PixelsPerFrame = Math.Clamp(
                pixelsPerFrame,
                Lumos.Domain.Zoom.Min,
                Lumos.Domain.Zoom.Max
            )
        };

    public ViewportState ZoomBy(double factor) =>
        SetZoom(PixelsPerFrame * factor);

    public ViewportState SetScroll(double x, double y) =>
        this with
        {
            ScrollX = Math.Max(0, x),
            ScrollY = Math.Max(0, y)
        };

    public ViewportState SetViewportSize(double width, double height) =>
        this with { ViewportWidth = width, ViewportHeight = height };

    public ViewportState ScrollToFrame(int frame, int totalFrames)
    {
        double targetX = frame * PixelsPerFrame;
        double maxX    = totalFrames * PixelsPerFrame;
        double halfW   = ViewportWidth / 2;
        return this with { ScrollX = Math.Clamp(targetX - halfW, 0, Math.Max(0, maxX - ViewportWidth)) };
    }

    public ViewportState FitAll(int totalFrames)
    {
        if (totalFrames <= 0 || ViewportWidth <= 0) return this;
        double ppf = (ViewportWidth - Lumos.Domain.Zoom.FitAllBuffer * 2) / totalFrames;
        return SetZoom(ppf) with { ScrollX = 0 };
    }
}
