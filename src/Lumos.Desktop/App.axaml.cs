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
    public static IReadOnlyList<AgentToolSchema> McpToolSchemas { get; private set; } = Array.Empty<AgentToolSchema>();
    private static readonly List<AgentMessage> _aiHistory = new();
    public static IReadOnlyList<AgentMessage> AiConversationHistory => _aiHistory;

    // FileSystem watcher for auto-import
    private static FolderWatcher? _folderWatcher;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        InitializeServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
            desktop.Exit += (_, _) =>
            {
                Autosave?.SaveNow();
                McpServer?.StopAsync().GetAwaiter().GetResult();
                McpServer?.Dispose();
                _folderWatcher?.Dispose();
                Autosave?.Dispose();
            };
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
        Autosave = new AutosaveService(EditorStore);
        Autosave.Start(projectDir);
        RecentProjects.RecordOpen(projectDir, "Untitled Project");

        // 5. Export & MCP
        Exporter  = new MediaExporter();
        McpServer = new McpServer(EditorStore, CommandQueue, Exporter, AssetManager);
        _ = McpServer.StartAsync();

        // 6. AI Agent (optional — requires API key)
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
            "Available tools: inspect_timeline, split_clip, trim_clip, move_clip, remove_clips, " +
            "ripple_delete, list_assets, import_media, export_video, generate_captions.";

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
}
