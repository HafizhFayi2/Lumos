using Lumos.Domain;

namespace Lumos.Application;

public interface IMediaExporter
{
    Task ExportAsync(Timeline timeline, ExportProfile profile, string outputPath, IProgress<double> progress);
}
