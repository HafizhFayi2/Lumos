using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Lumos.Desktop.Controls;

/// <summary>
/// Interactive playhead with scrub, frame info tooltip, loop markers, and drag handle.
/// Renders the vertical playhead line, triangular ruler handle, and current frame indicator.
/// </summary>
public sealed class TimelinePlayheadControl : Control
{
    // ── Styled properties ─────────────────────────────────────────────────

    public static readonly StyledProperty<double> ZoomScaleProperty =
        AvaloniaProperty.Register<TimelinePlayheadControl, double>(nameof(ZoomScale), 4.0);

    public static readonly StyledProperty<int> PlayheadFrameProperty =
        AvaloniaProperty.Register<TimelinePlayheadControl, int>(nameof(PlayheadFrame), 0);

    public static readonly StyledProperty<int> TotalFramesProperty =
        AvaloniaProperty.Register<TimelinePlayheadControl, int>(nameof(TotalFrames), 0);

    public static readonly StyledProperty<int> FpsProperty =
        AvaloniaProperty.Register<TimelinePlayheadControl, int>(nameof(Fps), 30);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<TimelinePlayheadControl, bool>(nameof(IsPlaying), false);

    public static readonly StyledProperty<int?> LoopInFrameProperty =
        AvaloniaProperty.Register<TimelinePlayheadControl, int?>(nameof(LoopInFrame));

    public static readonly StyledProperty<int?> LoopOutFrameProperty =
        AvaloniaProperty.Register<TimelinePlayheadControl, int?>(nameof(LoopOutFrame));

    public static readonly StyledProperty<bool> IsDraggingProperty =
        AvaloniaProperty.Register<TimelinePlayheadControl, bool>(nameof(IsDragging), false);

    public double ZoomScale { get => GetValue(ZoomScaleProperty); set => SetValue(ZoomScaleProperty, value); }
    public int PlayheadFrame { get => GetValue(PlayheadFrameProperty); set => SetValue(PlayheadFrameProperty, value); }
    public int TotalFrames { get => GetValue(TotalFramesProperty); set => SetValue(TotalFramesProperty, value); }
    public int Fps { get => GetValue(FpsProperty); set => SetValue(FpsProperty, value); }
    public bool IsPlaying { get => GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }
    public int? LoopInFrame { get => GetValue(LoopInFrameProperty); set => SetValue(LoopInFrameProperty, value); }
    public int? LoopOutFrame { get => GetValue(LoopOutFrameProperty); set => SetValue(LoopOutFrameProperty, value); }
    public bool IsDragging { get => GetValue(IsDraggingProperty); set => SetValue(IsDraggingProperty, value); }

    // ── Events ────────────────────────────────────────────────────────────

    public event EventHandler<int>? FrameSeeked;
    public event EventHandler<int>? DragStarted;
    public event EventHandler<int>? DragMoved;
    public event EventHandler<int>? DragEnded;

    // ── Constants ─────────────────────────────────────────────────────────

    private const double HandleWidth = 14.0;
    private const double HandleHeight = 12.0;
    private const double LineWidth = 1.5;
    private const double RulerHeight = 28.0;
    private const double HitTestWidth = 10.0;

    // ── Brushes ───────────────────────────────────────────────────────────

    private static readonly SolidColorBrush PlayheadLine = new(Color.Parse("#4FC3F7"));
    private static readonly SolidColorBrush PlayheadHandle = new(Color.Parse("#4FC3F7"));
    private static readonly SolidColorBrush PlayheadGlow = new(Color.Parse("#304FC3F7"));
    private static readonly SolidColorBrush LoopRegionFill = new(Color.Parse("#154FC3F7"));
    private static readonly SolidColorBrush LoopRegionBorder = new(Color.Parse("#404FC3F7"));
    private static readonly SolidColorBrush FrameInfoBg = new(Color.Parse("#E8161C26"));
    private static readonly SolidColorBrush FrameInfoBorder = new(Color.Parse("#4FC3F7"));
    private static readonly SolidColorBrush FrameInfoText = new(Color.Parse("#E1F5FE"));
    private static readonly Pen LinePen = new(PlayheadLine, LineWidth);
    private static readonly Pen LoopPen = new(LoopRegionBorder, 1.0);

    // ── State ─────────────────────────────────────────────────────────────

    private bool _isDragging;
    private double _dragStartX;
    private int _dragStartFrame;

    static TimelinePlayheadControl()
    {
        AffectsRender<TimelinePlayheadControl>(
            ZoomScaleProperty, PlayheadFrameProperty, IsPlayingProperty,
            LoopInFrameProperty, LoopOutFrameProperty, IsDraggingProperty);
        ClipToBoundsProperty.OverrideDefaultValue<TimelinePlayheadControl>(false);
    }

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);

        double height = Bounds.Height;
        if (height <= 0) return;

        double playheadX = PlayheadFrame * ZoomScale;
        double halfW = HandleWidth / 2;

        // ── Loop region highlight ─────────────────────────────────────
        if (LoopInFrame.HasValue && LoopOutFrame.HasValue)
        {
            double loopStart = LoopInFrame.Value * ZoomScale;
            double loopEnd = LoopOutFrame.Value * ZoomScale;
            if (loopEnd > loopStart)
            {
                ctx.DrawRectangle(LoopRegionFill, LoopPen,
                    new Rect(loopStart, RulerHeight, loopEnd - loopStart, height - RulerHeight));
            }
        }

        // ── Glow behind line ─────────────────────────────────────────
        ctx.DrawRectangle(PlayheadGlow, null,
            new Rect(playheadX - 4, 0, 8, height));

        // ── Main vertical line ───────────────────────────────────────
        ctx.DrawLine(LinePen,
            new Point(playheadX, RulerHeight),
            new Point(playheadX, height));

        // ── Ruler handle (triangle) ──────────────────────────────────
        var handleGeometry = new StreamGeometry();
        using (var sgCtx = handleGeometry.Open())
        {
            sgCtx.BeginFigure(new Point(playheadX - halfW, 0), true);
            sgCtx.LineTo(new Point(playheadX + halfW, 0));
            sgCtx.LineTo(new Point(playheadX + halfW, HandleHeight - 3));
            sgCtx.LineTo(new Point(playheadX, HandleHeight));
            sgCtx.LineTo(new Point(playheadX - halfW, HandleHeight - 3));
            sgCtx.EndFigure(true);
        }
        ctx.DrawGeometry(PlayheadHandle, new Pen(PlayheadLine, 1.0), handleGeometry);

        // ── Dragging indicator ───────────────────────────────────────
        if (_isDragging)
        {
            // Pulsing wider line during drag
            var dragPen = new Pen(PlayheadLine, 3.0, dashStyle: DashStyle.Dash);
            ctx.DrawLine(dragPen,
                new Point(playheadX, RulerHeight),
                new Point(playheadX, height));
        }

        // ── Frame info tooltip (shown on hover/drag) ─────────────────
        if (_isDragging)
        {
            string tc = FormatTimecode(PlayheadFrame, Fps);
            string frameInfo = $"F: {PlayheadFrame}  {tc}";

            var ft = new FormattedText(
                frameInfo,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas, Menlo, monospace"),
                11,
                FrameInfoText);

            double infoW = ft.Width + 16;
            double infoH = ft.Height + 8;
            double infoX = playheadX - infoW / 2;
            double infoY = HandleHeight + 4;

            ctx.DrawRectangle(FrameInfoBg, new Pen(FrameInfoBorder, 1.0),
                new RoundedRect(new Rect(infoX, infoY, infoW, infoH), 4));
            ctx.DrawText(ft, new Point(infoX + 8, infoY + 4));
        }
    }

    // ── Mouse interaction ─────────────────────────────────────────────────

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        double playheadX = PlayheadFrame * ZoomScale;

        // Hit test: near the playhead line
        if (Math.Abs(pos.X - playheadX) < HitTestWidth)
        {
            e.Pointer.Capture(this);
            _isDragging = true;
            _dragStartX = pos.X;
            _dragStartFrame = PlayheadFrame;
            IsDragging = true;
            DragStarted?.Invoke(this, PlayheadFrame);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);

        if (_isDragging)
        {
            double deltaX = pos.X - _dragStartX;
            int deltaFrame = (int)(deltaX / ZoomScale);
            int newFrame = Math.Clamp(_dragStartFrame + deltaFrame, 0, TotalFrames);
            PlayheadFrame = newFrame;
            DragMoved?.Invoke(this, newFrame);
            e.Handled = true;
        }
        else
        {
            double playheadX = PlayheadFrame * ZoomScale;
            Cursor = Math.Abs(pos.X - playheadX) < HitTestWidth
                ? new Cursor(StandardCursorType.SizeWestEast)
                : new Cursor(StandardCursorType.Arrow);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isDragging)
        {
            _isDragging = false;
            IsDragging = false;
            e.Pointer.Capture(null);
            DragEnded?.Invoke(this, PlayheadFrame);
            FrameSeeked?.Invoke(this, PlayheadFrame);
            e.Handled = true;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        int delta = e.Delta.Y > 0 ? 1 : e.Delta.Y < 0 ? -1 : 0;
        if (delta != 0)
        {
            int newFrame = Math.Clamp(PlayheadFrame + delta, 0, TotalFrames);
            FrameSeeked?.Invoke(this, newFrame);
            e.Handled = true;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string FormatTimecode(int frame, int fps)
    {
        if (fps <= 0) fps = 30;
        int hours = frame / (fps * 3600);
        int minutes = (frame / (fps * 60)) % 60;
        int seconds = (frame / fps) % 60;
        int frames = frame % fps;
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}:{frames:D2}";
    }
}
