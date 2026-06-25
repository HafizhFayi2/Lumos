namespace Palmier.Domain;

public sealed class Marker
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Label { get; set; } = string.Empty;
    public string Color { get; set; } = "#2986F6";
    public int Frame { get; set; }
}

public sealed class AssetFolder
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Folder";
    public string? ParentId { get; set; }
}
