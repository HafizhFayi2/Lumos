namespace Lumos.Domain;

public sealed class Track
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Track";
    public ClipType Type { get; set; } = ClipType.Video;
    public bool IsMuted { get; set; }
    public bool IsHidden { get; set; }
    public bool IsSyncLocked { get; set; } = true;

    /// Track-level volume multiplier (0.0 to 1.0). Applied on top of per-clip volume.
    public double Volume { get; set; } = 1.0;

    /// Track-level stereo pan (-1.0 = full left, 0.0 = center, 1.0 = full right).
    public double Pan { get; set; } = 0.0;

    public List<Clip> Clips { get; set; } = new();

    // Display-only, not persisted
    public double DisplayHeight { get; set; } = 50;

    public int EndFrame => Clips.Count == 0 ? 0 : Clips.Max(c => c.EndFrame);

    /// IDs of clips forming a contiguous chain starting at fromEnd, excluding excludeId.
    public HashSet<string> ContiguousClipIds(int fromEnd, string excludeId)
    {
        var ids = new HashSet<string>();
        int chainEnd = fromEnd;
        foreach (var c in Clips.OrderBy(c => c.StartFrame))
        {
            if (c.Id == excludeId || c.StartFrame < fromEnd) continue;
            if (c.StartFrame != chainEnd) break;
            chainEnd = c.EndFrame;
            ids.Add(c.Id);
        }
        return ids;
    }

    public Track Clone(bool newId = false)
    {
        var t = (Track)MemberwiseClone();
        if (newId) t.Id = Guid.NewGuid().ToString();
        t.Clips = Clips.Select(c => c.Clone(newId)).ToList();
        return t;
    }
}
