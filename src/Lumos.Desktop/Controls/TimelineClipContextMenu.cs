using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Lumos.Desktop.Controls;

/// <summary>
/// Context menu for timeline clip operations: split, trim, delete, duplicate,
/// apply effect, inspect, copy/paste, and more.
/// </summary>
public sealed class TimelineClipContextMenu : Control
{
    // ── Styled properties ─────────────────────────────────────────────────

    public static readonly StyledProperty<string?> ClipIdProperty =
        AvaloniaProperty.Register<TimelineClipContextMenu, string?>(nameof(ClipId));

    public static readonly StyledProperty<string?> ClipNameProperty =
        AvaloniaProperty.Register<TimelineClipContextMenu, string?>(nameof(ClipName));

    public static readonly StyledProperty<string?> TrackIdProperty =
        AvaloniaProperty.Register<TimelineClipContextMenu, string?>(nameof(TrackId));

    public static readonly StyledProperty<int> PlayheadFrameProperty =
        AvaloniaProperty.Register<TimelineClipContextMenu, int>(nameof(PlayheadFrame));

    public string? ClipId { get => GetValue(ClipIdProperty); set => SetValue(ClipIdProperty, value); }
    public string? ClipName { get => GetValue(ClipNameProperty); set => SetValue(ClipNameProperty, value); }
    public string? TrackId { get => GetValue(TrackIdProperty); set => SetValue(TrackIdProperty, value); }
    public int PlayheadFrame { get => GetValue(PlayheadFrameProperty); set => SetValue(PlayheadFrameProperty, value); }

    // ── Events ────────────────────────────────────────────────────────────

    public event EventHandler<string>? ActionRequested;

    // ── Menu items ────────────────────────────────────────────────────────

    private static readonly (string Id, string Label, string Shortcut, bool IsSeparator)[] MenuItems =
    {
        ("split", "Split at Playhead", "S", false),
        ("", "", "", true), // separator
        ("cut", "Cut", "Ctrl+X", false),
        ("copy", "Copy", "Ctrl+C", false),
        ("paste", "Paste", "Ctrl+V", false),
        ("duplicate", "Duplicate", "Ctrl+D", false),
        ("", "", "", true), // separator
        ("delete", "Delete", "Del", false),
        ("ripple_delete", "Ripple Delete", "Shift+Del", false),
        ("", "", "", true), // separator
        ("trim_start", "Trim Start to Playhead", "[", false),
        ("trim_end", "Trim End to Playhead", "]", false),
        ("", "", "", true), // separator
        ("speed_025", "Speed 0.25x", "", false),
        ("speed_050", "Speed 0.5x", "", false),
        ("speed_100", "Speed 1.0x (Normal)", "", false),
        ("speed_150", "Speed 1.5x", "", false),
        ("speed_200", "Speed 2.0x", "", false),
        ("", "", "", true), // separator
        ("effects", "Add Effect...", "", false),
        ("text", "Convert to Text Clip", "", false),
        ("", "", "", true), // separator
        ("inspector", "Show in Inspector", "I", false),
    };

    // ── State ─────────────────────────────────────────────────────────────

    private bool _isOpen;
    private int _hoveredIndex = -1;
    private double _menuX, _menuY;
    private const double ItemHeight = 28.0;
    private const double MenuWidth = 240.0;
    private const double SeparatorHeight = 9.0;
    private const double CornerSize = 8.0;

    // ── Brushes ───────────────────────────────────────────────────────────

    private static readonly SolidColorBrush MenuBg = new(Color.Parse("#212B38"));
    private static readonly SolidColorBrush MenuBorder = new(Color.Parse("#232C38"));
    private static readonly SolidColorBrush ItemHover = new(Color.Parse("#2986F620"));
    private static readonly SolidColorBrush TextPrimary = new(Color.Parse("#F4F8FC"));
    private static readonly SolidColorBrush TextSecondary = new(Color.Parse("#8593A6"));
    private static readonly SolidColorBrush SeparatorColor = new(Color.Parse("#232C38"));
    private static readonly SolidColorBrush ShortcutColor = new(Color.Parse("#5C6A7C"));
    private static readonly SolidColorBrush AccentColor = new(Color.Parse("#2986F6"));
    private static readonly SolidColorBrush DeleteColor = new(Color.Parse("#F87171"));

    static TimelineClipContextMenu()
    {
        AffectsRender<TimelineClipContextMenu>(ClipIdProperty, ClipNameProperty);
    }

    public void Open(double x, double y)
    {
        _menuX = x;
        _menuY = y;
        _isOpen = true;
        _hoveredIndex = -1;
        IsVisible = true;
        Focus();
        InvalidateVisual();
    }

    public void Close()
    {
        _isOpen = false;
        _hoveredIndex = -1;
        IsVisible = false;
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        if (!_isOpen) return;

        double menuH = CalculateMenuHeight();

        // ── Drop shadow ───────────────────────────────────────────────
        ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#40000000")), null,
            new RoundedRect(new Rect(_menuX + 2, _menuY + 2, MenuWidth, menuH), CornerSize));

        // ── Menu background ───────────────────────────────────────────
        ctx.DrawRectangle(MenuBg, new Pen(MenuBorder, 1.0),
            new RoundedRect(new Rect(_menuX, _menuY, MenuWidth, menuH), CornerSize));

        // ── Clip name header ──────────────────────────────────────────
        if (!string.IsNullOrEmpty(ClipName))
        {
            var headerFt = new FormattedText(
                ClipName,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
                11,
                TextSecondary);

            ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#1A212B")), null,
                new RoundedRect(new Rect(_menuX, _menuY, MenuWidth, 28), new CornerRadius(CornerSize, CornerSize, 0, 0)));
            ctx.DrawText(headerFt, new Point(_menuX + 12, _menuY + 7));

            // Separator after header
            ctx.DrawLine(new Pen(SeparatorColor, 1.0),
                new Point(_menuX + 8, _menuY + 28),
                new Point(_menuX + MenuWidth - 8, _menuY + 28));
        }

        double currentY = _menuY + (string.IsNullOrEmpty(ClipName) ? 0 : 32);
        int itemIndex = 0;

        foreach (var item in MenuItems)
        {
            if (item.IsSeparator)
            {
                ctx.DrawLine(new Pen(SeparatorColor, 0.5),
                    new Point(_menuX + 8, currentY + SeparatorHeight / 2),
                    new Point(_menuX + MenuWidth - 8, currentY + SeparatorHeight / 2));
                currentY += SeparatorHeight;
                continue;
            }

            bool isHovered = _hoveredIndex == itemIndex;
            bool isDelete = item.Id is "delete" or "ripple_delete";
            var textColor = isDelete ? DeleteColor : isHovered ? AccentColor : TextPrimary;

            if (isHovered)
            {
                ctx.DrawRectangle(ItemHover, null,
                    new RoundedRect(new Rect(_menuX + 4, currentY, MenuWidth - 8, ItemHeight), 4));
            }

            var labelFt = new FormattedText(
                item.Label,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal),
                12,
                textColor);
            ctx.DrawText(labelFt, new Point(_menuX + 12, currentY + 6));

            if (!string.IsNullOrEmpty(item.Shortcut))
            {
                var shortcutFt = new FormattedText(
                    item.Shortcut,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal),
                    10,
                    ShortcutColor);
                ctx.DrawText(shortcutFt, new Point(_menuX + MenuWidth - shortcutFt.Width - 12, currentY + 8));
            }

            currentY += ItemHeight;
            itemIndex++;
        }
    }

    // ── Mouse interaction ─────────────────────────────────────────────────

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isOpen) return;

        var pos = e.GetPosition(this);
        int prevHovered = _hoveredIndex;
        _hoveredIndex = HitTestItem(pos.X, pos.Y);

        if (prevHovered != _hoveredIndex)
            InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!_isOpen) return;

        var pos = e.GetPosition(this);
        int hit = HitTestItem(pos.X, pos.Y);

        if (hit >= 0 && hit < MenuItems.Length && !MenuItems[hit].IsSeparator)
        {
            ActionRequested?.Invoke(this, MenuItems[hit].Id);
            Close();
            e.Handled = true;
        }
        else
        {
            Close();
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!_isOpen) return;

        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private int HitTestItem(double x, double y)
    {
        if (x < _menuX || x > _menuX + MenuWidth) return -1;

        double menuStartY = _menuY + (string.IsNullOrEmpty(ClipName) ? 0 : 32);
        double currentY = menuStartY;
        int itemIndex = 0;

        foreach (var item in MenuItems)
        {
            if (item.IsSeparator)
            {
                currentY += SeparatorHeight;
                continue;
            }

            if (y >= currentY && y < currentY + ItemHeight)
                return itemIndex;

            currentY += ItemHeight;
            itemIndex++;
        }
        return -1;
    }

    private double CalculateMenuHeight()
    {
        double h = string.IsNullOrEmpty(ClipName) ? 0 : 32;
        foreach (var item in MenuItems)
        {
            h += item.IsSeparator ? SeparatorHeight : ItemHeight;
        }
        return h;
    }
}
