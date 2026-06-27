using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using Lumos.Application;
using Lumos.Application.Assets;
using Lumos.Application.Commands;
using Lumos.Application.State;
using Lumos.MCP.Tools;
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

    public bool IsRunning { get; private set; }

    public McpServer(EditorStore store, CommandQueue queue, IMediaExporter exporter, AssetManager assets)
    {
        _store    = store;
        _queue    = queue;
        _exporter = exporter;
        _assets   = assets;
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
                services.AddSingleton<TimelineTools>();
                services.AddSingleton<AssetTools>();
                services.AddSingleton<ExportTools>();
                services.AddSingleton<CaptionTools>();
                services.AddMcpServer(opts =>
                {
                    opts.ServerInfo = new() { Name = "lumos-desktop", Version = "1.0.0" };
                })
                .WithHttpTransport()
                .WithTools<TimelineTools>()
                .WithTools<AssetTools>()
                .WithTools<ExportTools>()
                .WithTools<CaptionTools>();
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
            ToolDefinitions.InspectTimeline  => await sp.GetRequiredService<TimelineTools>().InspectTimelineAsync(ct),
            ToolDefinitions.SplitClip        => await sp.GetRequiredService<TimelineTools>().SplitClipAsync(args, ct),
            ToolDefinitions.TrimClip         => await sp.GetRequiredService<TimelineTools>().TrimClipAsync(args, ct),
            ToolDefinitions.MoveClip         => await sp.GetRequiredService<TimelineTools>().MoveClipAsync(args, ct),
            ToolDefinitions.RemoveClips      => await sp.GetRequiredService<TimelineTools>().RemoveClipsAsync(args, ct),
            ToolDefinitions.RippleDelete     => await sp.GetRequiredService<TimelineTools>().RippleDeleteAsync(args, ct),
            ToolDefinitions.ListAssets       => await sp.GetRequiredService<AssetTools>().ListAssetsAsync(ct),
            ToolDefinitions.ImportMedia      => await sp.GetRequiredService<AssetTools>().ImportMediaAsync(args, ct),
            ToolDefinitions.ExportVideo      => await sp.GetRequiredService<ExportTools>().ExportVideoAsync(args, ct),
            ToolDefinitions.GenerateCaptions => await sp.GetRequiredService<CaptionTools>().GenerateCaptionsAsync(args, ct),
            ToolDefinitions.ApplyEffect      => await sp.GetRequiredService<TimelineTools>().ApplyEffectAsync(args, ct),
            _                                => McpToolHelpers.Error($"unknown tool: {toolName}"),
        };
    }

    public void Dispose()
    {
        _host?.Dispose();
        _host = null;
    }
}
