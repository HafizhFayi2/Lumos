using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using Lumos.Application;
using Lumos.Application.Assets;
using Lumos.Application.Commands;
using Lumos.Application.Generation;
using Lumos.Application.State;
using Lumos.MCP.Tools;
using Lumos.Media;
using static Lumos.MCP.Tools.McpToolHelpers;

namespace Lumos.MCP;

/// Embedded MCP HTTP server (SSE transport, port 19789).
/// Lifecycle: call Start() once when the app starts, Stop() on shutdown.
public sealed class McpServer : IDisposable
{
    public const int Port = 19789;

    private IHost? _host;
    private readonly EditorStore _store;
    private readonly CommandQueue _queue;
    private readonly IMediaExporter _exporter;
    private readonly AssetManager _assets;
    private readonly TranscriptCache _transcriptCache;
    private readonly AgentActionHistory _actionHistory;
    private readonly VideoEngine _videoEngine;
    private readonly GenerationService _generationService;
    private readonly ModelCatalog _modelCatalog;
    private readonly GenerationLog _generationLog;
    private readonly MediaFolderStore _folderStore;

    public bool IsRunning { get; private set; }

    public McpServer(EditorStore store, CommandQueue queue, IMediaExporter exporter, AssetManager assets,
        TranscriptCache transcriptCache, AgentActionHistory actionHistory, VideoEngine videoEngine,
        GenerationService generationService, ModelCatalog modelCatalog, GenerationLog generationLog,
        MediaFolderStore folderStore)
    {
        _store    = store;
        _queue    = queue;
        _exporter = exporter;
        _assets   = assets;
        _transcriptCache = transcriptCache;
        _actionHistory = actionHistory;
        _videoEngine = videoEngine;
        _generationService = generationService;
        _modelCatalog = modelCatalog;
        _generationLog = generationLog;
        _folderStore = folderStore;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_host is not null) return;

        var builder = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(_store);
                services.AddSingleton(_queue);
                services.AddSingleton(_exporter);
                services.AddSingleton(_assets);
                services.AddSingleton(_transcriptCache);
                services.AddSingleton(_actionHistory);
                services.AddSingleton(_videoEngine);
                services.AddSingleton(_generationService);
                services.AddSingleton(_modelCatalog);
                services.AddSingleton(_generationLog);
                services.AddSingleton<TimelineTools>();
                services.AddSingleton<AssetTools>();
                services.AddSingleton<ExportTools>();
                services.AddSingleton<CaptionTools>();
                services.AddSingleton<ProjectTools>();
                services.AddSingleton<AIEditingTools>();
                services.AddSingleton<GenerationTools>();
                services.AddSingleton(_folderStore);
                services.AddSingleton<MediaFolderTools>();
                services.AddMcpServer(opts =>
                {
                    opts.ServerInfo = new() { Name = "lumos-desktop", Version = "1.0.0" };
                })
                .WithHttpTransport()
                .WithTools<TimelineTools>()
                .WithTools<AssetTools>()
                .WithTools<ExportTools>()
                .WithTools<CaptionTools>()
                .WithTools<ProjectTools>()
                .WithTools<MediaFolderTools>()
                .WithTools<AIEditingTools>()
                .WithTools<GenerationTools>();
            })
            .ConfigureWebHostDefaults(web =>
            {
                web.UseUrls($"http://localhost:{Port}");
                web.Configure(app => 
                {
                    // Empty configuration just to satisfy GenericWebHostService.
                });
            });

        _host = builder.Build();
        await _host.StartAsync(ct);
        IsRunning = true;
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_host is null) return;
        await _host.StopAsync(ct);
        IsRunning = false;
    }

    /// Dispatch a tool call by name and JSON args dict.
    /// Used by AgentService to execute tool calls from the AI loop.
    public async Task<string> DispatchToolCallAsync(string toolName, JsonElement args, CancellationToken ct = default)
    {
        await using var scope = _host!.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        return toolName switch
        {
            ToolDefinitions.InspectTimeline    => await sp.GetRequiredService<TimelineTools>().InspectTimelineAsync(ct),
            ToolDefinitions.GetTimeline         => await sp.GetRequiredService<TimelineTools>().GetTimelineAsync(ct),
            ToolDefinitions.SplitClip          => await sp.GetRequiredService<TimelineTools>().SplitClipAsync(args, ct),
            ToolDefinitions.TrimClip           => await sp.GetRequiredService<TimelineTools>().TrimClipAsync(args, ct),
            ToolDefinitions.MoveClip           => await sp.GetRequiredService<TimelineTools>().MoveClipAsync(args, ct),
            ToolDefinitions.RemoveClips        => await sp.GetRequiredService<TimelineTools>().RemoveClipsAsync(args, ct),
            ToolDefinitions.RippleDelete       => await sp.GetRequiredService<TimelineTools>().RippleDeleteAsync(args, ct),
            ToolDefinitions.RippleDeleteRanges => await sp.GetRequiredService<TimelineTools>().RippleDeleteRangesAsync(args, ct),
            ToolDefinitions.AddClips           => await sp.GetRequiredService<TimelineTools>().AddClipsAsync(args, ct),
            ToolDefinitions.InsertClips        => await sp.GetRequiredService<TimelineTools>().InsertClipsAsync(args, ct),
            ToolDefinitions.ApplyEffect        => await sp.GetRequiredService<TimelineTools>().ApplyEffectAsync(args, ct),
            ToolDefinitions.ListAssets         => await sp.GetRequiredService<AssetTools>().ListAssetsAsync(ct),
            ToolDefinitions.GetMedia           => await sp.GetRequiredService<AssetTools>().GetMediaAsync(ct),
            ToolDefinitions.InspectMedia       => await sp.GetRequiredService<AssetTools>().InspectMediaAsync(args, ct),
            ToolDefinitions.DeleteMedia        => await sp.GetRequiredService<AssetTools>().DeleteMediaAsync(args, ct),
            ToolDefinitions.ImportMedia        => await sp.GetRequiredService<AssetTools>().ImportMediaAsync(args, ct),
            ToolDefinitions.ExportVideo        => await sp.GetRequiredService<ExportTools>().ExportVideoAsync(args, ct),
            ToolDefinitions.GenerateCaptions   => await sp.GetRequiredService<CaptionTools>().GenerateCaptionsAsync(args, ct),
            ToolDefinitions.SetProjectSettings => await sp.GetRequiredService<ProjectTools>().SetProjectSettingsAsync(args, ct),
            ToolDefinitions.ListMediaFolders   => await sp.GetRequiredService<MediaFolderTools>().ListMediaFoldersAsync(ct),
            ToolDefinitions.CreateMediaFolder  => await sp.GetRequiredService<MediaFolderTools>().CreateMediaFolderAsync(args, ct),
            ToolDefinitions.RenameMediaFolder  => await sp.GetRequiredService<MediaFolderTools>().RenameMediaFolderAsync(args, ct),
            ToolDefinitions.MoveMedia          => await sp.GetRequiredService<MediaFolderTools>().MoveMediaAsync(args, ct),
            ToolDefinitions.DeleteMediaFolder  => await sp.GetRequiredService<MediaFolderTools>().DeleteMediaFolderAsync(args, ct),
            // AI editing tools
            ToolDefinitions.SearchTranscript    => await sp.GetRequiredService<AIEditingTools>().SearchTranscriptAsync(args, ct),
            ToolDefinitions.GetTranscript       => await sp.GetRequiredService<AIEditingTools>().GetTranscriptAsync(args, ct),
            ToolDefinitions.DetectFillerWords   => await sp.GetRequiredService<AIEditingTools>().DetectFillerWordsAsync(ct),
            ToolDefinitions.RemoveFillerRegions => await sp.GetRequiredService<AIEditingTools>().RemoveFillerRegionsAsync(args, ct),
            ToolDefinitions.DetectHighlights    => await sp.GetRequiredService<AIEditingTools>().DetectHighlightsAsync(args, ct),
            ToolDefinitions.InspectFrame        => await sp.GetRequiredService<AIEditingTools>().InspectFrameAsync(args, ct),
            ToolDefinitions.GetActionHistory    => await sp.GetRequiredService<AIEditingTools>().GetActionHistoryAsync(args, ct),
            ToolDefinitions.AnalyzeSilences      => await sp.GetRequiredService<AIEditingTools>().AnalyzeSilencesAsync(args, ct),
            // Generative media tools
            ToolDefinitions.ListModels            => await sp.GetRequiredService<GenerationTools>().ListModelsAsync(args, ct),
            ToolDefinitions.GenerateMedia         => await sp.GetRequiredService<GenerationTools>().GenerateMediaAsync(args, ct),
            ToolDefinitions.GetGenerationStatus   => await sp.GetRequiredService<GenerationTools>().GetGenerationStatusAsync(args, ct),
            ToolDefinitions.GetGenerationLog      => await sp.GetRequiredService<GenerationTools>().GetGenerationLogAsync(ct),
            ToolDefinitions.SetModelApiKey        => await sp.GetRequiredService<GenerationTools>().SetModelApiKeyAsync(args, ct),
            _                                => McpToolHelpers.Error($"unknown tool: {toolName}"),
        };
    }

    public void Dispose()
    {
        _host?.Dispose();
        _host = null;
    }
}
