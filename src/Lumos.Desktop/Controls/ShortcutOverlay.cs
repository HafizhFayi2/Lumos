using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Lumos.Desktop.Controls;

/// <summary>
/// Semi-transparent overlay showing available keyboard shortcuts.
/// Toggled by pressing Ctrl+/ or ?. Displays categorized shortcut
/// reference for timeline editing, playback, and project operations.
/// </summary>
public sealed class ShortcutOverlay : Control
{
    public static readonly StyledProperty<bool> IsVisibleProperty =
        AvaloniaProperty.Register<ShortcutOverlay, bool>(nameof(IsVisible), false);

    public bool IsOpen
    {
        get => GetValue(IsVisibleProperty);
        set
        {
            SetValue(IsVisibleProperty, value);
            IsVisible = value;
            if (value) Focus();
            InvalidateVisual();
        }
    }

    // ── Shortcut data ───────────────────────────────────────────────

    private static readonly (string Category, (string Key, string Action)[] Shortcuts)[] Categories =
    {
        ("Playback", new (string, string)[]
        {
            ("Space", "Play / Pause"),
            ("K", "Play / Pause (alt)"),
            ("J", "Step backward 1 frame"),
            ("L", "Step forward 1 frame"),
            ("← / →", "Step 1 frame"),
            ("Shift+← / →", "Step 5 frames"),
            ("Home", "Go to start"),
            ("End", "Go to end"),
            ("Up / Down", "Prev / Next clip"),
        }),
        ("Editing", new (string, string)[]
        {
            ("V", "Pointer tool"),
            ("C", "Razor tool"),
            ("T", "Toggle tool (Pointer/Razor)"),
            ("S", "Split clip at playhead"),
            ("[", "Trim start to playhead"),
            ("]", "Trim end to playhead"),
            ("Del / Backspace", "Delete selected clip(s)"),
            ("Shift+Del", "Ripple delete"),
            ("Ctrl+D", "Duplicate clip"),
            ("M", "Add marker at playhead"),
        }),
        ("Selection & Nudge", new (string, string)[]
        {
            ("Click", "Select clip"),
            ("Shift+Click", "Toggle clip selection"),
            ("Drag", "Marquee select"),
            ("Ctrl+A", "Select all clips"),
            ("Alt+← / →", "Nudge 1 frame"),
            ("Alt+Shift+← / →", "Nudge 5 frames"),
        }),
        ("Project", new (string, string)[]
        {
            ("Ctrl+S", "Save project"),
            ("Ctrl+O", "Open project"),
            ("Ctrl+N", "New project"),
            ("Ctrl+Z", "Undo"),
            ("Ctrl+Y / Ctrl+Shift+Z", "Redo"),
            ("Ctrl+E", "Export"),
        }),
        ("Preview & View", new (string, string)[]
        {
            ("Ctrl+1", "Full resolution"),
            ("Ctrl+2", "Half resolution"),
            ("Ctrl+3", "Quarter resolution"),
            ("Ctrl+0", "Fit to window"),
            ("+ / =", "Zoom in"),
            ("-", "Zoom out"),
        }),
        ("General", new (string, string)[]
        {
            ("Ctrl+/ or ?", "Toggle this overlay"),
            ("I", "Show clip in Inspector"),
            ("Esc", "Close overlay / cancel"),
        }),
    };

    // ── Layout constants ────────────────────────────────────────────

    private const double OverlayWidth = 500;
    private const double OverlayMaxHeight = 520;
    private const double CategoryGap = 20;
    private const double RowHeight = 22;
    private const double ColumnGap = 16;
    private const double Padding = 20;
    private const double CornerSize = 12;

    // ── Brushes ─────────────────────────────────────────────────────

    private static readonly SolidColorBrush OverlayBg = new(Color.Parse("#E8121214"));
    private static readonly SolidColorBrush OverlayBorder = new(Color.Parse("#3f3f46"));
    private static readonly SolidColorBrush TitleBrush = new(Color.Parse("#f8fafc"));
    private static readonly SolidColorBrush CategoryBrush = new(Color.Parse("#94a3b8"));
    private static readonly SolidColorBrush KeyBrush = new(Color.Parse("#e2e8f0"));
    private static readonly SolidColorBrush ActionBrush = new(Color.Parse("#64748b"));
    private static readonly SolidColorBrush KeyBg = new(Color.Parse("#27272a"));
    private static readonly SolidColorBrush DividerBrush = new(Color.Parse("#27272a"));

    static ShortcutOverlay()
    {
        AffectsRender<ShortcutOverlay>(IsVisibleProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ShortcutOverlay>(true);
        FocusableProperty.OverrideDefaultValue<ShortcutOverlay>(true);
    }

    public void Toggle()
    {
        IsOpen = !IsOpen;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            IsOpen = false;
            e.Handled = true;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // Click outside overlay content closes it
        var pos = e.GetPosition(this);
        double ox = (Bounds.Width - OverlayWidth) / 2;
        double oy = (Bounds.Height - OverlayMaxHeight) / 2;
        if (pos.X < ox || pos.X > ox + OverlayWidth || pos.Y < oy || pos.Y > oy + OverlayMaxHeight)
        {
            IsOpen = false;
            e.Handled = true;
        }
    }

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        if (!IsOpen) return;

        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        // Dark backdrop
        ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#80000000")), null,
            new Rect(0, 0, w, h));

        // Overlay panel centered
        double ox = (w - OverlayWidth) / 2;
        double oy = (h - OverlayMaxHeight) / 2;

        // Shadow
        ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#40000000")), null,
            new RoundedRect(new Rect(ox + 2, oy + 2, OverlayWidth, OverlayMaxHeight), CornerSize));

        // Panel background
        ctx.DrawRectangle(OverlayBg, new Pen(OverlayBorder, 1.0),
            new RoundedRect(new Rect(ox, oy, OverlayWidth, OverlayMaxHeight), CornerSize));

        double contentX = ox + Padding;
        double contentY = oy + Padding;

        // Title
        var titleFt = new FormattedText(
            "Keyboard Shortcuts",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            14,
            TitleBrush);
        ctx.DrawText(titleFt, new Point(contentX, contentY));
        contentY += titleFt.Height + 8;

        // Divider
        ctx.DrawLine(new Pen(DividerBrush, 1.0),
            new Point(contentX, contentY),
            new Point(contentX + OverlayWidth - Padding * 2, contentY));
        contentY += 12;

        // Dual-column layout
        double leftColumnWidth = (OverlayWidth - Padding * 2 - ColumnGap) / 2;

        int colIndex = 0;
        foreach (var (category, shortcuts) in Categories)
        {
            double colX = contentX + colIndex * (leftColumnWidth + ColumnGap);

            // Category header
            var catFt = new FormattedText(
                category,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
                11,
                CategoryBrush);
            ctx.DrawText(catFt, new Point(colX, contentY));
            contentY += catFt.Height + 6;

            foreach (var (key, action) in shortcuts)
            {
                // Key badge
                double keyW = MeasureKeyWidth(key) + 8;
                double keyH = RowHeight - 6;
                var keyRect = new Rect(colX, contentY + 2, keyW, keyH);
                ctx.DrawRectangle(KeyBg, null, new RoundedRect(keyRect, 3));

                var keyFt = new FormattedText(
                    key,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Medium),
                    10,
                    KeyBrush);
                ctx.DrawText(keyFt, new Point(colX + 4, contentY + 4));

                // Action label
                var actionFt = new FormattedText(
                    action,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal),
                    11,
                    ActionBrush);
                ctx.DrawText(actionFt, new Point(colX + keyW + 10, contentY + 3));

                contentY += RowHeight;
            }

            // After a category, check if we need to move to next column
            if (colIndex == 0 && contentY + 80 > oy + OverlayMaxHeight)
            {
                colIndex = 1;
                // Reset Y but keep contentX
                contentY = oy + Padding + titleFt.Height + 8 + 12;
            }

            contentY += CategoryGap;
        }

        // Dismiss hint at bottom
        var dismissFt = new FormattedText(
            "Press Esc or click outside to close",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal),
            10,
            ActionBrush);
        ctx.DrawText(dismissFt, new Point(
            ox + (OverlayWidth - dismissFt.Width) / 2,
            oy + OverlayMaxHeight - Padding - dismissFt.Height));
    }

    private static double MeasureKeyWidth(string key)
    {
        // Rough character-width estimation for layout
        int len = key.Length;
        if (len <= 1) return 20;
        if (len <= 3) return 28;
        if (len <= 6) return 36;
        return Math.Min(48, len * 6);
    }
}
