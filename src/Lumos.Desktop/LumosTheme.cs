namespace Lumos.Desktop;

/// Central design-system constants matching Palmier Pro's AppTheme.
/// All UI styling MUST reference these values instead of hardcoded hex/numerics.
public static class LumosTheme
{
    // ── Colors ───────────────────────────────────────────────────────────
    public static class Color
    {
        // Backgrounds
        public const string BgApp        = "#09090b";
        public const string BgPanel      = "#18181b";
        public const string BgElevated   = "#121214";
        public const string BgHover      = "#27272a";
        public const string BgInput      = "#1e1e24";
        public const string BgOverlay    = "#CC000000";
        public const string BgBlack      = "#000000";
        public const string BgTabActive  = "#09090b";

        // Text
        public const string TextPrimary   = "#f8fafc";
        public const string TextSecondary = "#94a3b8";
        public const string TextTertiary  = "#64748b";
        public const string TextMuted     = "#475569";
        public const string TextAccent    = "#3b82f6";
        public const string TextGood      = "#10b981";

        // Borders
        public const string BorderDefault = "#3f3f46";
        public const string BorderSubtle  = "#27272a";
        public const string BorderAccent  = "#3b82f6";

        // Accent
        public const string AccentBlue    = "#3b82f6";
        public const string AccentBlueBg  = "#1e3a8a22";
        public const string AccentBlueHover = "#60a5fa";
        public const string AccentBluePressed = "#2563eb";

        // Semantic
        public const string Danger        = "#F87171";
        public const string Warning       = "#F59E0B";
        public const string Success       = "#10b981";
        public const string Purple        = "#8B5CF6";
        public const string PurpleBg      = "#291651";

        // Track
        public const string TrackVideoBg   = "#1A2744";
        public const string TrackAudioBg   = "#11291A";
        public const string TrackTextBg    = "#24133A";
        public const string TrackImageBg   = "#2A2015";
        public const string TrackVideoBorder = "#1E5CA8";
        public const string TrackAudioBorder = "#2E8B57";
        public const string TrackSelectedVideoBg   = "#1E3B5E";
        public const string TrackSelectedAudioBg   = "#1C452C";
        public const string TrackSelectedTextBg    = "#3C1F5E";
        public const string TrackSelectedImageBg   = "#3D3020";
        public const string TrackSelectedVideoBorder = "#2986F6";
        public const string TrackSelectedAudioBorder = "#4ADE80";

        // Playhead
        public const string PlayheadLine   = "#4FC3F7";
        public const string PlayheadHandle = "#4FC3F7";
        public const string PlayheadGlow   = "#304FC3F7";
        public const string PlayheadLabelBg = "#E8161C26";
        public const string PlayheadLabelText = "#E1F5FE";
        public const string SnapLine       = "#2986F6";
        public const string SnapLabelBg    = "#E8161C26";
        public const string SnapLabelText  = "#81D4FA";

        // Effects
        public const string ClipEffectBorder = "#60A5FA";
        public const string ClipEffectFill   = "#40FFFFFF";
        public const string ClipWaveform     = "#804ADE80";
        public const string AiBadgeBg   = "#291651";
        public const string AiBadgeFg   = "#A855F7";

        // Chat
        public const string ChatUserBg   = "#1E3B5E";
        public const string ChatUserText = "#F4F8FC";
        public const string ChatAiBg     = "#1C2430";
        public const string ChatAiText   = "#D7E3F0";
        public const string ChatTyping   = "#1e1e24";
    }

    // ── Spacing ──────────────────────────────────────────────────────────
    public static class Spacing
    {
        public const double Xxs  = 2;
        public const double Xs   = 4;
        public const double Sm   = 8;
        public const double Md   = 12;
        public const double Lg   = 16;
        public const double Xl   = 24;
        public const double Xxl  = 32;
        public const double Xxxl = 48;
    }

    // ── Font sizes ───────────────────────────────────────────────────────
    public static class FontSize
    {
        public const double Xxs  = 9;
        public const double Xs   = 10;
        public const double Sm   = 11;
        public const double Md   = 12;
        public const double Lg   = 13;
        public const double Xl   = 14;
        public const double Xxl  = 15;
        public const double Xxxl = 16;
        public const double Display = 18;
        public const double Timecode = 16;
    }

    // ── Font weights ─────────────────────────────────────────────────────
    public static class FontWeight
    {
        public const string Normal   = "Normal";
        public const string Medium   = "Medium";
        public const string SemiBold = "SemiBold";
        public const string Bold     = "Bold";
    }

    // ── Corner radii ─────────────────────────────────────────────────────
    public static class Radius
    {
        public const double Xs  = 2;
        public const double Sm  = 4;
        public const double Md  = 6;
        public const double Lg  = 8;
        public const double Xl  = 12;
        public const double Xxl = 16;
        public const double Xxxl = 20;
        public const double Round = 28;
        public const double Full = 9999;
    }

    // ── Border widths ────────────────────────────────────────────────────
    public static class BorderWidth
    {
        public const double Hairline = 0.5;
        public const double Thin     = 1.0;
        public const double Medium   = 1.5;
        public const double Thick    = 2.0;
    }

    // ── Opacity ──────────────────────────────────────────────────────────
    public static class Opacity
    {
        public const double Subtle  = 0.1;
        public const double Faint   = 0.3;
        public const double Muted   = 0.6;
        public const double Medium  = 0.7;
        public const double Strong  = 0.85;
        public const double Solid   = 1.0;
    }

    // ── Icon sizes ───────────────────────────────────────────────────────
    public static class IconSize
    {
        public const double Xs  = 14;
        public const double Sm  = 15;
        public const double Md  = 18;
        public const double Lg  = 20;
        public const double Xl  = 24;
        public const double Xxl = 56;
    }

    // ── Track dimensions ─────────────────────────────────────────────────
    public static class Track
    {
        public const double Height = 56;
        public const double RulerHeight = 28;
        public const double HeaderWidth = 120;
        public const double MinHeight = 32;
        public const double MaxHeight = 200;
        public const double TrimHandleWidth = 6;
        public const double FadeDrawWidth = 24;
    }

    // ── Window defaults ──────────────────────────────────────────────────
    public static class Window
    {
        public const double DefaultWidth  = 1280;
        public const double DefaultHeight = 720;
        public const double MinWidth  = 1200;
        public const double MinHeight = 680;
    }
}
