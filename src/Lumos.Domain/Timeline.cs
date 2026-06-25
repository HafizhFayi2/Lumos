namespace Palmier.Domain;

public sealed class Timeline
{
    public int Fps { get; set; } = 30;
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public bool SettingsConfigured { get; set; }
    public List<Track> Tracks { get; set; } = new();

    public int TotalFrames => Tracks.Count == 0 ? 0 : Tracks.Max(t => t.EndFrame);

    public Timeline Clone()
    {
        var t = (Timeline)MemberwiseClone();
        t.Tracks = Tracks.Select(tr => tr.Clone()).ToList();
        return t;
    }
}

/// Clip location within the track array.
public readonly record struct ClipLocation(int TrackIndex, int ClipIndex);
