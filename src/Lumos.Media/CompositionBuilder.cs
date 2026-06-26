using Lumos.Domain;

namespace Lumos.Media;

/// Resolves a Timeline + playhead frame into an ordered list of CompositionSlots.
/// Mirrors VideoEngine.rebuild() without any AVFoundation dependencies.
public sealed class CompositionBuilder
{
    private Timeline? _timeline;

    public void Load(Timeline timeline) => _timeline = timeline;

    /// Returns visual slots bottom-to-top and audio slots, for the given timeline frame.
    public CompositionFrame Build(int timelineFrame)
    {
        if (_timeline is null) return CompositionFrame.Empty;

        var visual = new List<CompositionSlot>();
        var audio  = new List<CompositionSlot>();

        foreach (var track in _timeline.Tracks)
        {
            if (track.IsHidden && track.Type.IsVisual()) continue;
            if (track.IsMuted  && track.Type == ClipType.Audio) continue;

            foreach (var clip in track.Clips)
            {
                if (!clip.Contains(timelineFrame)) continue;

                int localFrame  = timelineFrame - clip.StartFrame;
                int sourceFrame = clip.TrimStartFrame + (int)Math.Round(localFrame * clip.Speed);

                var slot = new CompositionSlot(
                    Clip:       clip,
                    AssetPath:  clip.MediaRef,
                    SourceFrame: sourceFrame,
                    Opacity:    clip.OpacityAt(timelineFrame),
                    Volume:     clip.VolumeAt(timelineFrame),
                    Transform:  clip.TransformAt(timelineFrame),
                    Crop:       clip.CropAt(timelineFrame),
                    RenderType: clip.MediaType,
                    Effects:    clip.Effects
                );

                if (clip.MediaType == ClipType.Audio)
                    audio.Add(slot);
                else
                    visual.Add(slot);
            }
        }

        // Visual tracks: lower index = further back; reverse for painter's order
        visual.Reverse();

        return new CompositionFrame(timelineFrame, visual, audio);
    }
}

public sealed record CompositionFrame(
    int TimelineFrame,
    IReadOnlyList<CompositionSlot> Visual,
    IReadOnlyList<CompositionSlot> Audio
)
{
    public static readonly CompositionFrame Empty =
        new(0, Array.Empty<CompositionSlot>(), Array.Empty<CompositionSlot>());
}
