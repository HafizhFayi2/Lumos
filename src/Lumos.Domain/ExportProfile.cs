namespace Lumos.Domain;

public class ExportProfile
{
    public string Name { get; set; } = "H.264 1080p60";
    public string Format { get; set; } = "mp4";
    public string VideoCodec { get; set; } = "libx264";
    public string AudioCodec { get; set; } = "aac";
    public int VideoBitrateKbps { get; set; } = 15000;
    public int AudioBitrateKbps { get; set; } = 320;
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public int FrameRate { get; set; } = 60;
}
