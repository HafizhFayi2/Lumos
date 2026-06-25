using Palmier.Domain;

namespace Palmier.Media;

/// One resolved clip entry for a given timeline frame.
public sealed record CompositionSlot(
    Clip Clip,
    string AssetPath,
    int SourceFrame,
    double Opacity,
    double Volume,
    Transform Transform,
    Crop Crop,
    ClipType RenderType
);
