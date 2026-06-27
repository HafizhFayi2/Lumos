using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumos.Desktop.Controls;

/// <summary>
/// Vertical snap indicator line that appears when clips are being dragged
/// and align with snap targets (other clip edges, playhead, markers).
/// </summary>
public sealed class SnapLineOverlay : Control
{
    public static readonly StyledProperty<double?> SnapLineXProperty =
        AvaloniaProperty.Register<SnapLineOverlay, double?>(nameof(SnapLineX));

    public static readonly StyledProperty<string> SnapLabelProperty =
        AvaloniaProperty.Register<SnapLineOverlay, string>(nameof(SnapLabel), "");

    public static readonly StyledProperty<int?> SnappedFrameProperty =
        AvaloniaProperty.Register<SnapLineOverlay, int?>(nameof(SnappedFrame));

    public static readonly StyledProperty<double> ZoomScaleProperty =
        AvaloniaProperty.Register<SnapLineOverlay, double>(nameof(ZoomScale), 4.0);

    public double? SnapLineX { get => GetValue(SnapLineXProperty); set => SetValue(SnapLineXProperty, value); }
    public string SnapLabel { get => GetValue(SnapLabelProperty); set => SetValue(SnapLabelProperty, value); }
    public int? SnappedFrame { get => GetValue(SnappedFrameProperty); set => SetValue(SnappedFrameProperty, value); }
    public double ZoomScale { get => GetValue(ZoomScaleProperty); set => SetValue(ZoomScaleProperty, value); }

    private static readonly SolidColorBrush LineBrush = new(Color.Parse("#2986F6"));
    private static readonly SolidColorBrush GlowBrush = new(Color.Parse("#202986F6"));
    private static readonly SolidColorBrush LabelBg = new(Color.Parse("#E8161C26"));
    private static readonly SolidColorBrush LabelFg = new(Color.Parse("#81D4FA"));
    private static readonly Pen SnapPen = new(LineBrush, 1.5, new DashStyle(new double[] { 4, 2 }, 0));

    static SnapLineOverlay()
    {
        AffectsRender<SnapLineOverlay>(SnapLineXProperty, SnappedFrameProperty, SnapLabelProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<SnapLineOverlay>(false);
    }

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        if (!SnapLineX.HasValue || !SnappedFrame.HasValue) return;

        double x = SnapLineX.Value;
        double height = Bounds.Height;
        if (height <= 0 || x < 0) return;

        // Glow
        ctx.DrawRectangle(GlowBrush, null, new Rect(x - 3, 0, 6, height));

        // Dashed line
        ctx.DrawLine(SnapPen, new Point(x, 0), new Point(x, height));

        // Frame label
        string label = string.IsNullOrEmpty(SnapLabel)
            ? $"F:{SnappedFrame.Value}"
            : SnapLabel;

        var ft = new FormattedText(
            label,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Consolas, Menlo, monospace"),
            10,
            LabelFg);

        double infoW = ft.Width + 10;
        double infoH = ft.Height + 6;
        double infoX = x - infoW / 2;
        double infoY = 2;

        ctx.DrawRectangle(LabelBg, new Pen(LineBrush, 1.0),
            new RoundedRect(new Rect(infoX, infoY, infoW, infoH), 3));
        ctx.DrawText(ft, new Point(infoX + 5, infoY + 3));
    }
}
