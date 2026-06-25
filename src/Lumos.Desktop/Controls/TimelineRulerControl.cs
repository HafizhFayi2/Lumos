using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lumos.Domain;

namespace Lumos.Desktop.Controls;

/// Renders timeline ruler ticks and timecode labels using DrawingContext.
/// Driven by ZoomScale, Fps, and ScrollOffset styled properties.
public sealed class TimelineRulerControl : Control
{
    public static readonly StyledProperty<double> ZoomScaleProperty =
        AvaloniaProperty.Register<TimelineRulerControl, double>(nameof(ZoomScale), defaultValue: 4.0);

    public static readonly StyledProperty<int> FpsProperty =
        AvaloniaProperty.Register<TimelineRulerControl, int>(nameof(Fps), defaultValue: 30);

    public static readonly StyledProperty<double> ScrollOffsetProperty =
        AvaloniaProperty.Register<TimelineRulerControl, double>(nameof(ScrollOffset), defaultValue: 0.0);

    public double ZoomScale
    {
        get => GetValue(ZoomScaleProperty);
        set => SetValue(ZoomScaleProperty, value);
    }

    public int Fps
    {
        get => GetValue(FpsProperty);
        set => SetValue(FpsProperty, value);
    }

    public double ScrollOffset
    {
        get => GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    static TimelineRulerControl()
    {
        AffectsRender<TimelineRulerControl>(ZoomScaleProperty, FpsProperty, ScrollOffsetProperty);
    }

    public override void Render(DrawingContext ctx)
    {
        double width  = Bounds.Width;
        double height = Bounds.Height;

        if (width <= 0 || height <= 0) return;

        var ticks = TimelineRuler.GetTicks(width, ScrollOffset, ZoomScale, Math.Max(1, Fps));

        var majorBrush = new SolidColorBrush(Color.FromArgb(255, 92, 106, 124));
        var minorBrush = new SolidColorBrush(Color.FromArgb(120, 92, 106, 124));
        var labelBrush = new SolidColorBrush(Color.FromArgb(255, 92, 106, 124));

        var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);
        double fontSize = 9;

        foreach (var tick in ticks)
        {
            bool isMajor = tick.Type == TickType.Major;
            var pen = new Pen(isMajor ? majorBrush : minorBrush, 1.0);

            double tickTop = height - tick.Height;
            ctx.DrawLine(pen, new Point(tick.X, tickTop), new Point(tick.X, height));

            if (isMajor && !string.IsNullOrEmpty(tick.Timecode))
            {
                var ft = new FormattedText(
                    tick.Timecode,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    fontSize,
                    labelBrush);

                double labelX = tick.X + 2;
                ctx.DrawText(ft, new Point(labelX, 2));
            }
        }
    }
}
