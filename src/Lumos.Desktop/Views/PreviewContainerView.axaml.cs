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
    private WriteableBitmap? _previewBitmap;
    private const int PreviewW = 960;
    private const int PreviewH = 540;

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
                _previewBitmap ??= new WriteableBitmap(
                    new PixelSize(PreviewW, PreviewH),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Premul);

                using (var buf = _previewBitmap.Lock())
                {
                    Marshal.Copy(pixelData, 0, buf.Address, Math.Min(pixelData.Length, buf.RowBytes * PreviewH));
                }

                PreviewImage.Source = _previewBitmap;

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
