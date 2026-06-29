using System.Threading;
using System.Threading.Tasks;
using Lumos.Domain;

namespace Lumos.Application;

public interface IThumbnailGenerator
{
    Task<string> GenerateThumbnailAsync(Asset asset, TimeSpan position, CancellationToken ct = default);
    Task<List<string>> GenerateWaveformAsync(Asset asset, CancellationToken ct = default);
}
