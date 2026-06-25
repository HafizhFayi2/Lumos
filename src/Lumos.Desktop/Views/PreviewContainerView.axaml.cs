using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Lumos.Media;

namespace Lumos.Desktop.Views;

public partial class PreviewContainerView : UserControl
{
    private VideoEngine? _videoEngine;

    public PreviewContainerView()
    {
        InitializeComponent();
    }

    public void ConnectVideoEngine(VideoEngine videoEngine, int fps)
    {
        if (_videoEngine != null)
        {
            _videoEngine.FrameComposited -= OnFrameComposited;
        }

        _videoEngine = videoEngine;
        _videoEngine.FrameComposited += OnFrameComposited;
    }

    private void OnFrameComposited(int frame, byte[] pixelData)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                int width = 1920;
                int height = 1080;
                
                var writeableBitmap = new WriteableBitmap(
                    new PixelSize(width, height),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Premul);

                using (var buf = writeableBitmap.Lock())
                {
                    Marshal.Copy(pixelData, 0, buf.Address, pixelData.Length);
                }

                PreviewImage.Source = writeableBitmap;

                // Update timecode
                int fps = 30; // Default or fetched from state
                int hours = frame / (fps * 3600);
                int minutes = (frame / (fps * 60)) % 60;
                int seconds = (frame / fps) % 60;
                int frames = frame % fps;
                TimecodeText.Text = $"{hours:D2}:{minutes:D2}:{seconds:D2}:{frames:D2}";
            }
            catch
            {
                // Suppress UI update exceptions during tearing down
            }
        });
    }
}
