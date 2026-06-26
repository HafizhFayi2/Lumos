using System.Threading.Tasks;
using Lumos.Domain;
using Lumos.Infrastructure;
using Lumos.Media;
using Xunit;

namespace Lumos.Tests.Infrastructure;

public class SkiaCompositorTests
{
    [Fact]
    public async Task CompositeAsync_UsesFrameProviderForVideoSlots()
    {
        var provider = new SolidFrameProvider(8, 6);
        using var compositor = new SkiaCompositor(8, 6, provider);
        var clip = new Clip
        {
            Id = "clip-1",
            MediaRef = "video.mp4",
            MediaType = ClipType.Video,
            StartFrame = 0,
            DurationFrames = 10
        };
        var slot = new CompositionSlot(
            clip,
            clip.MediaRef,
            SourceFrame: 3,
            Opacity: 1,
            Volume: 1,
            Transform: new Transform(),
            Crop: new Crop(),
            RenderType: ClipType.Video,
            Effects: []
        );

        var pixels = await compositor.CompositeAsync(new CompositionFrame(3, [slot], []));

        Assert.True(provider.WasCalled);
        Assert.Equal(8 * 6 * 4, pixels.Length);
        Assert.Equal(10, pixels[0]);
        Assert.Equal(20, pixels[1]);
        Assert.Equal(30, pixels[2]);
        Assert.Equal(255, pixels[3]);
    }

    private sealed class SolidFrameProvider : IFrameProvider
    {
        private readonly int _width;
        private readonly int _height;

        public bool WasCalled { get; private set; }

        public SolidFrameProvider(int width, int height)
        {
            _width = width;
            _height = height;
        }

        public Task<byte[]?> GetFrameAsync(string assetPath, int sourceFrame, int width, int height)
        {
            WasCalled = true;
            var pixels = new byte[_width * _height * 4];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 10;
                pixels[i + 1] = 20;
                pixels[i + 2] = 30;
                pixels[i + 3] = 255;
            }

            return Task.FromResult<byte[]?>(pixels);
        }
    }
}
