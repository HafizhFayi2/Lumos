using System.Threading.Tasks;

namespace Lumos.Media;

/// Turns a CompositionFrame into raw BGRA pixel bytes.
public interface IFrameCompositor
{
    Task<byte[]> CompositeAsync(CompositionFrame frame);
}
