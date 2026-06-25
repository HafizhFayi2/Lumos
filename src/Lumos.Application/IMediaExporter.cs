using Palmier.Domain;

namespace Palmier.Application;

public interface IMediaExporter
{
    Task ExportAsync(Timeline timeline, ExportProfile profile, string outputPath, IProgress<double> progress);
}
