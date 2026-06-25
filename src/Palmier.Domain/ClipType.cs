namespace Palmier.Domain;

public enum ClipType
{
    Video,
    Audio,
    Text,
    Image,
    Lottie
}

public static class ClipTypeExtensions
{
    public static bool IsCompatible(this ClipType source, ClipType target) =>
        source switch
        {
            ClipType.Video or ClipType.Image or ClipType.Lottie or ClipType.Text =>
                target is ClipType.Video or ClipType.Image or ClipType.Lottie or ClipType.Text,
            ClipType.Audio => target == ClipType.Audio,
            _ => false
        };

    public static bool IsVisual(this ClipType type) =>
        type is ClipType.Video or ClipType.Image or ClipType.Lottie or ClipType.Text;
}
