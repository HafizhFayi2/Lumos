using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Palmier.Application.Assets;
using Palmier.Application.Commands;
using Palmier.Application.State;
using Palmier.Domain;
using Palmier.Infrastructure;
using Palmier.Media;
using Palmier.Desktop.ViewModels;
using Palmier.Desktop.Views;

namespace Palmier.Desktop;

public partial class App : Avalonia.Application
{
    public static EditorStore EditorStore { get; private set; } = null!;
    public static CommandQueue CommandQueue { get; private set; } = null!;
    public static AssetManager AssetManager { get; private set; } = null!;
    public static VideoEngine VideoEngine { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        InitializeServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeServices()
    {
        // 1. Core State Store
        EditorStore = new EditorStore();

        // 2. Commands Execution Pipeline
        CommandQueue = new CommandQueue(EditorStore);

        // 3. Media Processing Infrastructure
        var cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ThumbnailsCache");
        var thumbnailGenerator = new ThumbnailGenerator(cacheDir);
        AssetManager = new AssetManager(thumbnailGenerator);

        var frameCache = new FrameCache();
        var frameProvider = new FrameProvider(frameCache);
        var seekController = new SeekController();
        VideoEngine = new VideoEngine(EditorStore, frameProvider, seekController);

        // 4. Initial Timeline Setup
        var timeline = new Timeline { Fps = 30, Width = 1920, Height = 1080 };
        timeline.Tracks.Add(new Track { Id = "V2", Name = "V2", Type = ClipType.Video });
        timeline.Tracks.Add(new Track { Id = "V1", Name = "V1", Type = ClipType.Video });
        timeline.Tracks.Add(new Track { Id = "A1", Name = "A1", Type = ClipType.Audio });
        timeline.Tracks.Add(new Track { Id = "A2", Name = "A2", Type = ClipType.Audio });

        var projectId = Guid.NewGuid();
        EditorStore.SetProject(projectId, "Untitled Project", timeline);
        VideoEngine.Rebuild();
    }
}
