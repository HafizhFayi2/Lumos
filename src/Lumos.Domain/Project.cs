namespace Palmier.Domain;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Untitled Project";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    
    public int FrameRate { get; set; } = 60;
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;

    public List<Timeline> Timelines { get; set; } = new();
}
