using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Lumos.Desktop.Controls;

/// <summary>
/// Track header control with mute, hide, lock, rename, and track type indicator.
/// Renders in the static left column of the timeline.
/// </summary>
public sealed class TrackHeaderControl : Control
{
    // ── Styled properties ─────────────────────────────────────────────────

    public static readonly StyledProperty<string> TrackNameProperty =
        AvaloniaProperty.Register<TrackHeaderControl, string>(nameof(TrackName), "Track");

    public static readonly StyledProperty<string> TrackTypeProperty =
        AvaloniaProperty.Register<TrackHeaderControl, string>(nameof(TrackType), "VIDEO");

    public static readonly StyledProperty<bool> IsMutedProperty =
        AvaloniaProperty.Register<TrackHeaderControl, bool>(nameof(IsMuted), false);

    public static readonly StyledProperty<bool> IsHiddenProperty =
        AvaloniaProperty.Register<TrackHeaderControl, bool>(nameof(IsHidden), false);

    public static readonly StyledProperty<bool> IsLockedProperty =
        AvaloniaProperty.Register<TrackHeaderControl, bool>(nameof(IsLocked), false);

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<TrackHeaderControl, bool>(nameof(IsSelected), false);

    public static readonly StyledProperty<string> TrackIdProperty =
        AvaloniaProperty.Register<TrackHeaderControl, string>(nameof(TrackId), "");

    public string TrackName { get => GetValue(TrackNameProperty); set => SetValue(TrackNameProperty, value); }
    public string TrackType { get => GetValue(TrackTypeProperty); set => SetValue(TrackTypeProperty, value); }
    public bool IsMuted { get => GetValue(IsMutedProperty); set => SetValue(IsMutedProperty, value); }
    public bool IsHidden { get => GetValue(IsHiddenProperty); set => SetValue(IsHiddenProperty, value); }
    public bool IsLocked { get => GetValue(IsLockedProperty); set => SetValue(IsLockedProperty, value); }
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public string TrackId { get => GetValue(TrackIdProperty); set => SetValue(TrackIdProperty, value); }

    // ── Events ────────────────────────────────────────────────────────────

    public event EventHandler? MuteToggled;
    public event EventHandler? HideToggled;
    public event EventHandler? LockToggled;
    public event EventHandler? TrackSelected;

    // ── Constants ─────────────────────────────────────────────────────────

    private const double IconSize = 18.0;
    private const double IconGap = 3.0;
    private const double TopPadding = 8.0;

    // ── Brushes ───────────────────────────────────────────────────────────

    private static readonly SolidColorBrush BgDefault = new(Color.Parse("#18181B"));
    private static readonly SolidColorBrush BgSelected = new(Color.Parse("#1E293B"));
    private static readonly SolidColorBrush BorderBrush = new(Color.Parse("#27272A"));
    private static readonly SolidColorBrush TextPrimary = new(Color.Parse("#F8FAFC"));
    private static readonly SolidColorBrush TextSecondary = new(Color.Parse("#64748B"));
    private static readonly SolidColorBrush TextMuted = new(Color.Parse("#475569"));
    private static readonly SolidColorBrush MuteActive = new(Color.Parse("#F59E0B"));
    private static readonly SolidColorBrush HideActive = new(Color.Parse("#EF4444"));
    private static readonly SolidColorBrush LockActive = new(Color.Parse("#8B5CF6"));
    private static readonly SolidColorBrush TypeVideo = new(Color.Parse("#3B82F6"));
    private static readonly SolidColorBrush TypeAudio = new(Color.Parse("#10B981"));
    private static readonly SolidColorBrush TypeText = new(Color.Parse("#A855F7"));
    private static readonly SolidColorBrush TypeImage = new(Color.Parse("#F59E0B"));
    private static readonly SolidColorBrush IconIdle = new(Color.Parse("#64748B"));
    private static readonly SolidColorBrush SelectionGlow = new(Color.Parse("#202986F6"));

    // ── Button hit areas ──────────────────────────────────────────────────

    private Rect _muteRect, _hideRect, _lockRect;
    private bool _hoverMute, _hoverHide, _hoverLock;

    static TrackHeaderControl()
    {
        AffectsRender<TrackHeaderControl>(
            TrackNameProperty, TrackTypeProperty, IsMutedProperty,
            IsHiddenProperty, IsLockedProperty, IsSelectedProperty);
    }

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);

        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        // ── Background ────────────────────────────────────────────────
        var bg = IsSelected ? BgSelected : BgDefault;
        ctx.DrawRectangle(bg, null, new Rect(0, 0, w, h));

        // ── Selection glow ────────────────────────────────────────────
        if (IsSelected)
        {
            ctx.DrawRectangle(SelectionGlow, null, new Rect(0, 0, w, h));
        }

        // ── Right border ──────────────────────────────────────────────
        ctx.DrawLine(new Pen(BorderBrush, 1.0), new Point(w - 1, 0), new Point(w - 1, h));

        // ── Track type color bar ──────────────────────────────────────
        var typeColor = GetTrackTypeColor();
        ctx.DrawRectangle(typeColor, null, new Rect(0, 0, 3, h));

        // ── Track name ────────────────────────────────────────────────
        var nameFt = new FormattedText(
            TrackName,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold),
            11,
            IsMuted || IsHidden ? TextMuted : TextPrimary);
        ctx.DrawText(nameFt, new Point(10, TopPadding));

        // ── Track type label ──────────────────────────────────────────
        var typeFt = new FormattedText(
            TrackType.ToUpperInvariant(),
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold),
            8,
            typeColor);
        ctx.DrawText(typeFt, new Point(10, TopPadding + 16));

        // ── Control buttons row ───────────────────────────────────────
        double btnY = TopPadding + 32;
        double btnX = 10;

        // Mute button (M)
        _muteRect = new Rect(btnX, btnY, IconSize, IconSize);
        DrawIconButton(ctx, _muteRect, "M", IsMuted ? MuteActive : IconIdle, _hoverMute, IsMuted);
        btnX += IconSize + IconGap;

        // Hide button (H)
        _hideRect = new Rect(btnX, btnY, IconSize, IconSize);
        DrawIconButton(ctx, _hideRect, "H", IsHidden ? HideActive : IconIdle, _hoverHide, IsHidden);
        btnX += IconSize + IconGap;

        // Lock button (L)
        _lockRect = new Rect(btnX, btnY, IconSize, IconSize);
        DrawIconButton(ctx, _lockRect, "L", IsLocked ? LockActive : IconIdle, _hoverLock, IsLocked);

        // ── Bottom separator ──────────────────────────────────────────
        ctx.DrawLine(new Pen(BorderBrush, 0.5), new Point(0, h - 0.5), new Point(w, h - 0.5));
    }

    private void DrawIconButton(DrawingContext ctx, Rect rect, string label,
        SolidColorBrush color, bool isHover, bool isActive)
    {
        if (isHover || isActive)
        {
            var hoverBg = new SolidColorBrush(Color.FromArgb(40, color.Color.R, color.Color.G, color.Color.B));
            ctx.DrawRectangle(hoverBg, new Pen(color, 0.5), new RoundedRect(rect, 3));
        }

        var ft = new FormattedText(
            label,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold),
            9,
            color);
        ctx.DrawText(ft, new Point(rect.X + 4, rect.Y + 3));
    }

    // ── Mouse interaction ─────────────────────────────────────────────────

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);

        bool prevMute = _hoverMute;
        bool prevHide = _hoverHide;
        bool prevLock = _hoverLock;

        _hoverMute = _muteRect.Contains(pos);
        _hoverHide = _hideRect.Contains(pos);
        _hoverLock = _lockRect.Contains(pos);

        if (_hoverMute || _hoverHide || _hoverLock)                Cursor = new Cursor(StandardCursorType.Hand);
        else
                Cursor = new Cursor(StandardCursorType.Arrow);

        if (prevMute != _hoverMute || prevHide != _hoverHide || prevLock != _hoverLock)
            InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);

        if (_muteRect.Contains(pos))
        {
            MuteToggled?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if (_hideRect.Contains(pos))
        {
            HideToggled?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if (_lockRect.Contains(pos))
        {
            LockToggled?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        else
        {
            TrackSelected?.Invoke(this, EventArgs.Empty);
        }
    }

    private SolidColorBrush GetTrackTypeColor() => TrackType.ToUpperInvariant() switch
    {
        "AUDIO" => TypeAudio,
        "TEXT" => TypeText,
        "IMAGE" => TypeImage,
        _ => TypeVideo,
    };
}
