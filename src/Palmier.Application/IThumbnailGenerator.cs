using Palmier.Domain;

namespace Palmier.Application;

public interface IThumbnailGenerator
{
    Task<string> GenerateThumbnailAsync(Asset asset, TimeSpan position);
    Task<List<string>> GenerateWaveformAsync(Asset asset);
}
