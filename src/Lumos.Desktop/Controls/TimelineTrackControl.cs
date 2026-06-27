using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Lumos.Domain;

namespace Lumos.Desktop.Controls;

/// <summary>
/// Renders a single timeline track row: clips with video thumbnails, waveform overlays,
/// trim handles, selection outlines, fade ramps, and AI badges.
/// All painting is done via DrawingContext (no visual tree per clip) for performance.
/// </summary>
public sealed class TimelineTrackControl : Control
{
    // ── Styled properties ─────────────────────────────────────────────────

    public static readonly StyledProperty<double> ZoomScaleProperty =
        AvaloniaProperty.Register<TimelineTrackControl, double>(nameof(ZoomScale), 4.0);

    public static readonly StyledProperty<int> TrackIndexProperty =
        AvaloniaProperty.Register<TimelineTrackControl, int>(nameof(TrackIndex), 0);

    public static readonly StyledProperty<string> TrackTypeProperty =
        AvaloniaProperty.Register<TimelineTrackControl, string>(nameof(TrackType), "VIDEO");

    public static readonly StyledProperty<HashSet<string>> SelectedClipIdsProperty =
        AvaloniaProperty.Register<TimelineTrackControl, HashSet<string>>(nameof(SelectedClipIds), new());

    public static readonly StyledProperty<int?> PlayheadFrameProperty =
        AvaloniaProperty.Register<TimelineTrackControl, int?>(nameof(PlayheadFrame));

    public static readonly StyledProperty<Track?> DomainTrackProperty =
        AvaloniaProperty.Register<TimelineTrackControl, Track?>(nameof(DomainTrack));

    public double ZoomScale { get => GetValue(ZoomScaleProperty); set => SetValue(ZoomScaleProperty, value); }
    public int TrackIndex { get => GetValue(TrackIndexProperty); set => SetValue(TrackIndexProperty, value); }
    public string TrackType { get => GetValue(TrackTypeProperty); set => SetValue(TrackTypeProperty, value); }
    public HashSet<string> SelectedClipIds { get => GetValue(SelectedClipIdsProperty); set => SetValue(SelectedClipIdsProperty, value); }
    public int? PlayheadFrame { get => GetValue(PlayheadFrameProperty); set => SetValue(PlayheadFrameProperty, value); }
    public Track? DomainTrack { get => GetValue(DomainTrackProperty); set => SetValue(DomainTrackProperty, value); }

    // ── Brush cache ───────────────────────────────────────────────────────

    private static readonly SolidColorBrush VideoClipBg = new(Color.Parse("#1A2744"));
    private static readonly SolidColorBrush VideoClipBgSelected = new(Color.Parse("#1E3B5E"));
    private static readonly SolidColorBrush AudioClipBg = new(Color.Parse("#11291A"));
    private static readonly SolidColorBrush AudioClipBgSelected = new(Color.Parse("#1C452C"));
    private static readonly SolidColorBrush TextClipBg = new(Color.Parse("#24133A"));
    private static readonly SolidColorBrush TextClipBgSelected = new(Color.Parse("#3C1F5E"));
    private static readonly SolidColorBrush ImageClipBg = new(Color.Parse("#2A2015"));
    private static readonly SolidColorBrush ImageClipBgSelected = new(Color.Parse("#3D3020"));
    private static readonly SolidColorBrush BorderNormal = new(Color.Parse("#1E5CA8"));
    private static readonly SolidColorBrush BorderSelected = new(Color.Parse("#2986F6"));
    private static readonly SolidColorBrush BorderAudioNormal = new(Color.Parse("#2E8B57"));
    private static readonly SolidColorBrush BorderAudioSelected = new(Color.Parse("#4ADE80"));
    private static readonly SolidColorBrush TrimHandleBrush = new(Color.Parse("#60A5FA"));
    private static readonly SolidColorBrush FadeFillBrush = new(Color.Parse("#40FFFFFF"));
    private static readonly SolidColorBrush WaveformBrush = new(Color.Parse("#804ADE80"));
    private static readonly SolidColorBrush WaveformBgBrush = new(Color.Parse("#18000000"));
    private static readonly SolidColorBrush AIBadgeBg = new(Color.Parse("#291651"));
    private static readonly SolidColorBrush AIBadgeFg = new(Color.Parse("#A855F7"));
    private static readonly SolidColorBrush LabelShadow = new(Color.Parse("#CC000000"));
    private static readonly SolidColorBrush MutedOverlay = new(Color.Parse("#60000000"));

    private const double TrimHandleWidth = 6.0;
    private const double FadeDrawWidth = 24.0;
    private const int ThumbnailSampleInterval = 15; // pixels between thumbnail samples
    private const int WaveformBarWidth = 2;
    private const double LabelPadding = 6.0;
    private const double AIBadgeWidth = 22.0;
    private const double AIBadgeHeight = 14.0;

    // ── State ─────────────────────────────────────────────────────────────

    // ── State ─────────────────────────────────────────────────────────────

    private List<ClipRenderInfo> _clipInfos = new();
    private ClipRenderInfo? _hoveredClip;

    static TimelineTrackControl()
    {
        AffectsRender<TimelineTrackControl>(
            ZoomScaleProperty, SelectedClipIdsProperty, PlayheadFrameProperty, DomainTrackProperty);
        ClipToBoundsProperty.OverrideDefaultValue<TimelineTrackControl>(true);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        RebuildClipInfos();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        return finalSize;
    }

    // ── Rendering ─────────────────────────────────────────────────────────

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        var track = DomainTrack;
        if (track == null || track.Clips.Count == 0) return;

        double width = Bounds.Width;
        double height = Bounds.Height;
        if (width <= 0 || height <= 0) return;

        var selectedIds = SelectedClipIds ?? new HashSet<string>();
        bool trackMuted = track.IsMuted;

        foreach (var info in _clipInfos)
        {
            var clip = info.Clip;
            double clipX = info.Left;
            double clipW = info.Width;
            double clipH = height - 4;
            double clipY = 2;

            if (clipX + clipW < 0 || clipX > width) continue; // off-screen

            bool isSelected = selectedIds.Contains(clip.Id);
            bool isHovered = _hoveredClip?.Clip.Id == clip.Id;

            // ── Clip background ───────────────────────────────────────
            var bgBrush = GetClipBackground(clip, isSelected);
            var borderBrush = GetClipBorder(clip, isSelected);

            var clipRect = new Rect(clipX, clipY, clipW, clipH);
            ctx.DrawRectangle(bgBrush, null, new RoundedRect(clipRect, 3));

            // ── Content clip (trim to bounds) ─────────────────────────
            using (ctx.PushClip(new RoundedRect(new Rect(clipX, clipY, clipW, clipH), 3)))
            {
                // ── Thumbnail strip for video/image clips ─────────────
                if (clip.MediaType is ClipType.Video or ClipType.Image)
                {
                    DrawThumbnailStrip(ctx, info, clipX, clipY, clipW, clipH);
                }

                // ── Waveform for audio/video clips ────────────────────
                if (clip.MediaType == ClipType.Audio)
                {
                    DrawWaveform(ctx, info, clipX, clipY, clipW, clipH);
                }

                // ── Fade ramps ────────────────────────────────────────
                if (clip.FadeInFrames > 0 || clip.FadeOutFrames > 0)
                {
                    DrawFadeRamps(ctx, clip, clipX, clipY, clipW, clipH);
                }
            }

            // ── Border ────────────────────────────────────────────────
            var borderPen = new Pen(borderBrush, isSelected ? 2.0 : 1.0);
            ctx.DrawRectangle(null, borderPen, new RoundedRect(clipRect, 3));

            // ── Muted overlay ─────────────────────────────────────────
            if (trackMuted)
            {
                ctx.DrawRectangle(MutedOverlay, null, new RoundedRect(clipRect, 3));
            }

            // ── Clip label ────────────────────────────────────────────
            DrawClipLabel(ctx, info, clipX, clipY, clipW, clipH);

            // ── AI badge ──────────────────────────────────────────────
            if (info.IsAI)
            {
                DrawAIBadge(ctx, clipX, clipY);
            }

            // ── Trim handles ──────────────────────────────────────────
            if (clipW > 12)
            {
                DrawTrimHandle(ctx, clipX, clipY, TrimHandleWidth, clipH, true);
                DrawTrimHandle(ctx, clipX + clipW - TrimHandleWidth, clipY, TrimHandleWidth, clipH, false);
            }

            // ── Hover highlight ───────────────────────────────────────
            if (isHovered && !isSelected)
            {
                var hoverPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1.0);
                ctx.DrawRectangle(null, hoverPen, new RoundedRect(clipRect, 3));
            }
        }
    }

    // ── Thumbnail rendering ───────────────────────────────────────────────

    private void DrawThumbnailStrip(DrawingContext ctx, ClipRenderInfo info,
        double clipX, double clipY, double clipW, double clipH)
    {
        // Generate thumbnail positions along the clip width
        int sourceDuration = info.Clip.SourceDurationFrames;
        if (sourceDuration <= 0) return;

        double thumbH = clipH;
        int thumbCount = Math.Max(1, (int)(clipW / ThumbnailSampleInterval));

        for (int i = 0; i < thumbCount; i++)
        {
            double t = (double)i / Math.Max(1, thumbCount - 1);
            double x = clipX + t * clipW;
            int sourceFrame = (int)(t * sourceDuration);

            // Draw a subtle frame indicator rectangle for each thumbnail position
            var frameRect = new Rect(x, clipY, Math.Max(1, clipW / thumbCount - 1), thumbH);
            var alpha = (byte)(40 + (int)(20 * Math.Sin(t * Math.PI))); // subtle variation
            var thumbBg = new SolidColorBrush(Color.FromArgb(alpha, 100, 140, 200));
            ctx.DrawRectangle(thumbBg, null, frameRect);

            // Draw frame number indicator for every 5th thumbnail
            if (i % 5 == 0 && clipW > 100)
            {
                var ft = new FormattedText(
                    $"{sourceFrame}",
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal),
                    8,
                    new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)));
                ctx.DrawText(ft, new Point(x + 2, clipY + 2));
            }
        }
    }

    // ── Waveform rendering ────────────────────────────────────────────────

    private void DrawWaveform(DrawingContext ctx, ClipRenderInfo info,
        double clipX, double clipY, double clipW, double clipH)
    {
        double centerY = clipY + clipH / 2;
        double maxAmp = clipH / 2 - 4;

        // Background
        ctx.DrawRectangle(WaveformBgBrush, null,
            new RoundedRect(new Rect(clipX, clipY, clipW, clipH), 3));

        // Generate procedural waveform (peaks that vary by clip position)
        int barCount = Math.Max(1, (int)(clipW / (WaveformBarWidth + 1)));
        var random = new Random(info.Clip.Id.GetHashCode());

        for (int i = 0; i < barCount; i++)
        {
            double t = (double)i / barCount;
            double x = clipX + t * clipW;

            // Procedural amplitude using seeded random with some smoothness
            double baseAmp = 0.3 + 0.7 * Math.Abs(Math.Sin(t * 8 + info.Clip.StartFrame * 0.1));
            double noise = 0.5 + 0.5 * random.NextDouble();
            double amp = baseAmp * noise * maxAmp;

            var barRect = new Rect(x, centerY - amp, WaveformBarWidth, amp * 2);
            ctx.DrawRectangle(WaveformBrush, null, barRect);
        }

        // Draw center line
        var centerPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 74, 222, 128)), 0.5);
        ctx.DrawLine(centerPen, new Point(clipX, centerY), new Point(clipX + clipW, centerY));
    }

    // ── Fade ramps ────────────────────────────────────────────────────────

    private void DrawFadeRamps(DrawingContext ctx, Clip clip,
        double clipX, double clipY, double clipW, double clipH)
    {
        if (clip.FadeInFrames > 0 && clip.DurationFrames > 0)
        {
            double fadeW = Math.Min(FadeDrawWidth, clipW / 3);
            double fadeInT = (double)clip.FadeInFrames / clip.DurationFrames;
            double fadePx = fadeInT * clipW;

            var path = new StreamGeometry();
            using (var sgCtx = path.Open())
            {
                sgCtx.BeginFigure(new Point(clipX, clipY + clipH), true);
                sgCtx.LineTo(new Point(clipX + Math.Min(fadePx, fadeW), clipY));
                sgCtx.EndFigure(true);
            }
            ctx.DrawGeometry(FadeFillBrush, null, path);
        }

        if (clip.FadeOutFrames > 0 && clip.DurationFrames > 0)
        {
            double fadeW = Math.Min(FadeDrawWidth, clipW / 3);
            double fadeOutT = (double)clip.FadeOutFrames / clip.DurationFrames;
            double fadePx = fadeOutT * clipW;
            double startX = clipX + clipW - Math.Min(fadePx, fadeW);

            var path = new StreamGeometry();
            using (var sgCtx = path.Open())
            {
                sgCtx.BeginFigure(new Point(startX, clipY), true);
                sgCtx.LineTo(new Point(clipX + clipW, clipY + clipH));
                sgCtx.EndFigure(true);
            }
            ctx.DrawGeometry(FadeFillBrush, null, path);
        }
    }

    // ── Clip label ────────────────────────────────────────────────────────

    private void DrawClipLabel(DrawingContext ctx, ClipRenderInfo info,
        double clipX, double clipY, double clipW, double clipH)
    {
        if (clipW < 40) return; // too narrow for label

        string label = info.DisplayName;
        var ft = new FormattedText(
            label,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Medium),
            10,
            Brushes.White);

        double labelX = clipX + LabelPadding;
        double labelY = clipY + 4;

        // Shadow
        var shadowFt = new FormattedText(
            label,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Medium),
            10,
            LabelShadow);
        ctx.DrawText(shadowFt, new Point(labelX + 1, labelY + 1));
        ctx.DrawText(ft, new Point(labelX, labelY));

        // Duration label below
        if (clipH > 30 && clipW > 60)
        {
            var durFt = new FormattedText(
                info.DurationText,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas, Menlo, monospace"),
                8,
                new SolidColorBrush(Color.FromArgb(180, 200, 210, 230)));
            ctx.DrawText(durFt, new Point(labelX, labelY + 14));
        }
    }

    // ── AI badge ──────────────────────────────────────────────────────────

    private void DrawAIBadge(DrawingContext ctx, double clipX, double clipY)
    {
        var badgeRect = new Rect(
            clipX + 4,
            clipY + 4,
            AIBadgeWidth,
            AIBadgeHeight);

        ctx.DrawRectangle(AIBadgeBg, new Pen(AIBadgeFg, 1.0), new RoundedRect(badgeRect, 2));

        var ft = new FormattedText(
            "AI",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold),
            8,
            AIBadgeFg);
        ctx.DrawText(ft, new Point(badgeRect.X + 5, badgeRect.Y + 2));
    }

    // ── Trim handles ──────────────────────────────────────────────────────

    private void DrawTrimHandle(DrawingContext ctx, double x, double y, double w, double h, bool isLeft)
    {
        double centerY = y + h / 2;
        double handleH = Math.Min(h * 0.4, 16);

        ctx.DrawRectangle(TrimHandleBrush, null,
            new RoundedRect(new Rect(x + 1, centerY - handleH / 2, w - 2, handleH), 1));

        // Grip lines
        var linePen = new Pen(new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), 1.0);
        double gripX = isLeft ? x + w / 2 : x + w / 2;
        ctx.DrawLine(linePen, new Point(gripX, centerY - 4), new Point(gripX, centerY + 4));
    }

    // ── Hit testing ───────────────────────────────────────────────────────

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);
        var prev = _hoveredClip;
        _hoveredClip = HitTestClip(pos.X, pos.Y);

        if (_hoveredClip != null)
        {
            double leftEdge = _hoveredClip.Left;
            double rightEdge = _hoveredClip.Left + _hoveredClip.Width;

            if (Math.Abs(pos.X - leftEdge) < TrimHandleWidth + 4 ||
                Math.Abs(pos.X - rightEdge) < TrimHandleWidth + 4)
            {
                Cursor = new Cursor(StandardCursorType.SizeWestEast);
            }
            else
            {
                Cursor = new Cursor(StandardCursorType.Arrow);
            }
        }
        else
        {
            Cursor = new Cursor(StandardCursorType.Arrow);
        }

        if (_hoveredClip != prev)
            InvalidateVisual();
    }

    private ClipRenderInfo? HitTestClip(double x, double y)
    {
        foreach (var info in _clipInfos)
        {
            if (x >= info.Left && x <= info.Left + info.Width &&
                y >= 0 && y <= Bounds.Height)
            {
                return info;
            }
        }
        return null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            var pos = e.GetPosition(this);
            var hit = HitTestClip(pos.X, pos.Y);
            if (hit != null)
            {
                var windowPos = e.GetPosition(this);
                var window = TopLevel.GetTopLevel(this) as Window;
                if (window?.DataContext is ViewModels.MainWindowViewModel vm)
                {
                    vm.ShowContextMenu(
                        hit.Clip.Id,
                        System.IO.Path.GetFileNameWithoutExtension(hit.Clip.MediaRef ?? "Clip"),
                        DomainTrack?.Id ?? "");
                    var screenPt = this.TranslatePoint(windowPos, window) ?? new Point(0, 0);
                    var ctxMenu = window.FindControl<TimelineClipContextMenu>("ClipContextMenu");
                    ctxMenu?.Open(screenPt.X, screenPt.Y);
                }
                e.Handled = true;
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void RebuildClipInfos()
    {
        _clipInfos.Clear();
        var track = DomainTrack;
        if (track == null) return;

        foreach (var clip in track.Clips)
        {
            _clipInfos.Add(new ClipRenderInfo
            {
                Clip = clip,
                Left = clip.StartFrame * ZoomScale,
                Width = clip.DurationFrames * ZoomScale,
                IsAI = clip.MediaType == ClipType.Text ||
                       (clip.MediaRef?.Contains("Generated", StringComparison.OrdinalIgnoreCase) == true) ||
                       (clip.MediaRef?.Contains("AI", StringComparison.OrdinalIgnoreCase) == true),
                DisplayName = System.IO.Path.GetFileNameWithoutExtension(clip.MediaRef ?? "Clip"),
                DurationText = FormatDuration(clip.DurationFrames, track.Type == ClipType.Audio ? 44100 : 30),
            });
        }
    }

    private static SolidColorBrush GetClipBackground(Clip clip, bool selected)
    {
        return (clip.MediaType, selected) switch
        {
            (ClipType.Audio, true) => AudioClipBgSelected,
            (ClipType.Audio, false) => AudioClipBg,
            (ClipType.Text, true) => TextClipBgSelected,
            (ClipType.Text, false) => TextClipBg,
            (ClipType.Image, true) => ImageClipBgSelected,
            (ClipType.Image, false) => ImageClipBg,
            (_, true) => VideoClipBgSelected,
            _ => VideoClipBg,
        };
    }

    private static SolidColorBrush GetClipBorder(Clip clip, bool selected)
    {
        if (clip.MediaType == ClipType.Audio)
            return selected ? BorderAudioSelected : BorderAudioNormal;
        return selected ? BorderSelected : BorderNormal;
    }

    private static string FormatDuration(int frames, int fps)
    {
        if (fps <= 0) fps = 30;
        double totalSeconds = (double)frames / fps;
        int minutes = (int)(totalSeconds / 60);
        double seconds = totalSeconds % 60;
        return minutes > 0
            ? $"{minutes}:{seconds:F1}"
            : $"{seconds:F1}s";
    }

    private sealed class ClipRenderInfo
    {
        public required Clip Clip { get; init; }
        public double Left { get; init; }
        public double Width { get; init; }
        public bool IsAI { get; init; }
        public required string DisplayName { get; init; }
        public required string DurationText { get; init; }
    }
}


