using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lumos.AI;
using Lumos.Application.Assets;
using Lumos.Application.Commands;
using Lumos.Application.State;
using Lumos.Domain;
using Lumos.Application;
using Lumos.Application.Generation;
using Lumos.Infrastructure;
using Lumos.Media;
using Lumos.Desktop.ViewModels;
using Lumos.Desktop.Views;
using Lumos.MCP;

namespace Lumos.Desktop;

public partial class App : Avalonia.Application
{
    public static EditorStore EditorStore { get; private set; } = null!;
    public static CommandQueue CommandQueue { get; private set; } = null!;
    public static AssetManager AssetManager { get; private set; } = null!;
    public static VideoEngine VideoEngine { get; private set; } = null!;
    public static McpServer McpServer { get; private set; } = null!;
    public static IMediaExporter Exporter { get; private set; } = null!;

    // Persistence
    public static ProjectSerializer ProjectSerializer { get; private set; } = new();
    public static AutosaveService Autosave { get; private set; } = null!;
    public static RecentProjectsService RecentProjects { get; private set; } = new();

    // AI: null when no API key is configured
    public static AgentService? AgentService { get; private set; }
    public static SemanticSearchService SemanticSearch { get; private set; } = new(new VectorSearchEngine());
    public static AgentActionHistory ActionHistory { get; private set; } = new();
    public static TranscriptCache TranscriptCache { get; private set; } = new();
    public static ModelCatalog ModelCatalog { get; private set; } = new();
    public static GenerationService GenerationService { get; private set; } = null!;
    public static GenerationLog GenerationLog { get; private set; } = new();
    public static MediaFolderStore MediaFolderStore { get; private set; } = new();
    public static IReadOnlyList<AgentToolSchema> McpToolSchemas { get; private set; } = Array.Empty<AgentToolSchema>();
    private static readonly List<AgentMessage> _aiHistory = new();
    public static IReadOnlyList<AgentMessage> AiConversationHistory => _aiHistory;

    // Settings (loaded in Program.Main before Initialize is called)
    public static LumosSettings? Settings { get; set; }

    // Latest available update info (populated by background check)
    public static UpdateInfo? PendingUpdate { get; private set; }

    // FileSystem watcher for auto-import
    private static FolderWatcher? _folderWatcher;

    public override void Initialize()
    {
        // Apply saved window size before UI loads
        if (Settings?.Window != null)
        {
            // Window position is applied in OnFrameworkInitializationCompleted
        }

        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        InitializeServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };

            // Restore window position from settings
            if (Settings?.Window != null)
            {
                var ws = Settings.Window;
                mainWindow.Width = ws.Width;
                mainWindow.Height = ws.Height;
                if (ws.X != 0 || ws.Y != 0)
                {
                    mainWindow.Position = new Avalonia.PixelPoint((int)ws.X, (int)ws.Y);
                }
                if (ws.Maximized)
                    mainWindow.WindowState = Avalonia.Controls.WindowState.Maximized;
            }

            // Set app icon on the window
            var iconPath = AppIcon.EnsureIconExists();
            if (iconPath != null)
            {
                mainWindow.Icon = new Avalonia.Controls.WindowIcon(iconPath);
            }

            desktop.MainWindow = mainWindow;

            desktop.Exit += (_, _) =>
            {
                // Save window position
                if (Settings != null && desktop.MainWindow != null)
                {
                    var w = desktop.MainWindow;
                    Settings.Window = new WindowSettings
                    {
                        X = w.Position.X,
                        Y = w.Position.Y,
                        Width = w.Width,
                        Height = w.Height,
                        Maximized = w.WindowState == Avalonia.Controls.WindowState.Maximized,
                    };
                    Settings.Save();
                }

                Autosave?.SaveNow();
                McpServer?.StopAsync().GetAwaiter().GetResult();
                McpServer?.Dispose();
                _folderWatcher?.Dispose();
                Autosave?.Dispose();
            };

            // Auto-open the last project if configured
            if (Settings?.AutoOpenLastProject == true && !string.IsNullOrEmpty(Settings?.LastProjectPath))
            {
                var vm = (MainWindowViewModel)mainWindow.DataContext!;
                if (Directory.Exists(Settings.LastProjectPath))
                {
                    vm.OpenProject(Settings.LastProjectPath);
                }
            }

            // Check for updates on startup (non-blocking)
            if (Settings?.CheckForUpdates == true)
            {
                _ = CheckForUpdatesAsync();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeServices()
    {
        // 1. Core state
        EditorStore  = new EditorStore();
        CommandQueue = new CommandQueue(EditorStore);

        // 2. Media infrastructure
        var cacheDir          = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ThumbnailsCache");
        var thumbnailGenerator = new ThumbnailGenerator(cacheDir);
        AssetManager = new AssetManager(thumbnailGenerator);

        var frameCache    = new FrameCache();
        var frameProvider = new FrameProvider(frameCache);
        var seekController = new SeekController();
        var compositor    = new SkiaCompositor(960, 540, frameProvider);
        VideoEngine = new VideoEngine(EditorStore, frameProvider, seekController, compositor);

        // 3. Initial timeline
        var timeline = new Timeline { Width = 1920, Height = 1080, Fps = 30 };
        var projectId = Guid.NewGuid();
        EditorStore.SetProject(projectId, "Untitled Project", timeline);
        VideoEngine.Rebuild();

        // 4. Autosave
        var projectDir = GetDefaultProjectDir();
        Autosave = new AutosaveService(EditorStore, MediaFolderStore, AssetManager);
        Autosave.Start(projectDir);
        RecentProjects.RecordOpen(projectDir, "Untitled Project");

        // 5. Generative media service
        GenerationService = new GenerationService(ModelCatalog, AssetManager);

        // Register simulated providers for development (no API keys needed)
        ModelCatalog.RegisterProvider(new SimulatedImageProvider(),
            new ModelConfig("simulated-image-v1", "Simulated Image", ModelProviderType.Image, "Simulated Image"));
        ModelCatalog.RegisterProvider(new SimulatedVideoProvider(),
            new ModelConfig("simulated-video-v1", "Simulated Video", ModelProviderType.Video, "Simulated Video"));
        ModelCatalog.RegisterProvider(new SimulatedAudioProvider(),
            new ModelConfig("simulated-audio-v1", "Simulated Audio", ModelProviderType.Audio, "Simulated Audio"));

        // 6. Export & MCP
        Exporter  = new MediaExporter();
        McpServer = new McpServer(EditorStore, CommandQueue, Exporter, AssetManager, TranscriptCache, ActionHistory, VideoEngine,
            GenerationService, ModelCatalog, GenerationLog, MediaFolderStore);
        _ = McpServer.StartAsync();

        // 7. AI Agent (optional — requires API key)
        InitializeAgentService();

        // 8. Folder watcher
        InitializeFolderWatcher(projectId);

        // 9. Kick off background semantic indexing whenever a new asset is imported
        AssetManager.AssetAdded += (_, e) =>
        {
            _ = Task.Run(() => SemanticSearch.IndexAsset(e.Asset.FilePath));
        };
    }

    private static void InitializeAgentService()
    {
        var apiKey = AnthropicClient.LoadApiKey();
        if (string.IsNullOrWhiteSpace(apiKey)) return;

        var client = new AnthropicClient(apiKey, AnthropicModel.Sonnet46);

        // Tool dispatcher forwards AI tool calls to the MCP server
        Func<string, JsonElement, CancellationToken, Task<string>> dispatcher =
            (name, args, ct) => McpServer.DispatchToolCallAsync(name, args, ct);

        const string systemPrompt =
            "You are Lumos AI, an intelligent co-editor embedded in a professional video editor. " +
            "You can inspect and modify the timeline, import media, and export video using the provided tools. " +
            "Be direct and concise. When performing actions, always call the appropriate tool. " +
            "\n\nWhen the user mentions @media, @clip, or @timeline, use the available tools to " +
            "look up the current project context and respond with relevant information. " +
            "\n\nKey capabilities:\n" +
            "- Inspect timeline and media with inspect_timeline, get_timeline, get_media, inspect_media\n" +
            "- Edit clips with split_clip, trim_clip, move_clip, remove_clips\n" +
            "- Add/insert clips with add_clips, insert_clips, import_media\n" +
            "- Search with search_transcript and detect highlights/filler words\n" +
            "- Export video with export_video\n" +
            "- Verify actions with get_action_history and inspect_frame\n" +
            "- Manage media folders with list_media_folders, create_media_folder, move_media\n" +
            "- Manage projects with set_project_settings";

        AgentService = new AgentService(() => client, dispatcher, systemPrompt);

        // Build tool schemas from ToolDefinitions
        McpToolSchemas = BuildToolSchemas();
    }

    private static IReadOnlyList<AgentToolSchema> BuildToolSchemas()
    {
        var schemas = new List<AgentToolSchema>();
        foreach (var (name, description) in ToolDefinitions.Descriptions)
        {
            // Minimal input schema — tools use their own validation inside McpServer
            var inputSchema = new { type = "object", properties = new { }, required = Array.Empty<string>() };
            schemas.Add(new AgentToolSchema(name, description, inputSchema));
        }
        return schemas;
    }

    private static string GetDefaultProjectDir()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var dir = Path.Combine(docs, "LumosProjects", "current");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void InitializeFolderWatcher(Guid projectId)
    {
        var watchPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "LumosProjects", "Assets");

        try
        {
            Directory.CreateDirectory(watchPath);
            _folderWatcher = new FolderWatcher();
            _folderWatcher.FileArrived += async (_, path) =>
            {
                try
                {
                    await AssetManager.ImportAssetAsync(projectId, path);
                }
                catch { /* log dropped silently */ }
            };
            _folderWatcher.Watch(watchPath);
        }
        catch { /* watcher is non-critical */ }
    }

    public static void AppendAiHistory(string userText, string assistantText)
    {
        _aiHistory.Add(new AgentMessage(AgentRole.User,
            new[] { new MessageBlock.Text(userText) }));
        if (!string.IsNullOrEmpty(assistantText))
            _aiHistory.Add(new AgentMessage(AgentRole.Assistant,
                new[] { new MessageBlock.Text(assistantText) }));

        // Keep last 20 turns in memory
        while (_aiHistory.Count > 40) _aiHistory.RemoveAt(0);
    }

    /// Check remote appcast for a newer version and store result in PendingUpdate.
    private static async Task CheckForUpdatesAsync()
    {
        try
        {
            var info = await UpdateService.CheckForUpdateAsync();
            if (info != null)
            {
                PendingUpdate = info;
                // Log to MCP activity log so the user sees it
                if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow?.DataContext is MainWindowViewModel vm)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    vm.McpActivityLogs.Insert(0,
                        $"[{DateTime.Now:HH:mm:ss}] Update available: v{info.Version} — run 'Check for Updates' in Help menu.");
                });
            }
            }
        }
        catch
        {
            // Silent: update check is non-critical
        }
    }
}
