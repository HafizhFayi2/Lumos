using Palmier.Domain;

namespace Palmier.Application.State;

public abstract record PreviewTab
{
    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public abstract ClipType ClipType { get; }
    public abstract bool IsCloseable { get; }

    public sealed record TimelineTab : PreviewTab
    {
        public override string Id => "Timeline";
        public override string DisplayName => "Timeline";
        public override ClipType ClipType => ClipType.Video;
        public override bool IsCloseable => false;
    }

    public sealed record MediaAssetTab(string AssetId, string AssetName, ClipType Type) : PreviewTab
    {
        public override string Id => $"Asset_{AssetId}";
        public override string DisplayName => AssetName;
        public override ClipType ClipType => Type;
        public override bool IsCloseable => true;
    }

    public static readonly PreviewTab Timeline = new TimelineTab();
}
