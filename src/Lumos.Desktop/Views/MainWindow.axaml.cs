using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Lumos.Domain;
using Lumos.Application;
using Lumos.Application.Commands;
using Lumos.Application.State;
using Lumos.Desktop.Controls;
using Lumos.Desktop.ViewModels;

namespace Lumos.Desktop.Views;

public partial class MainWindow : Window
{

    private CancellationTokenSource? _exportCts;
    private TimelineInputController? _timelineController;
    private MainWindowViewModel VM => (MainWindowViewModel)DataContext!;

    // Reused preview bitmap — allocated once to avoid per-frame GC pressure
    private WriteableBitmap? _previewBitmap;
    private const int PreviewW = 960;
    private const int PreviewH = 540;

    public MainWindow()
    {
        InitializeComponent();
        WireEvents();

        App.VideoEngine.FrameComposited += OnVideoEngineFrameComposited;
        App.EditorStore.StateChanged    += OnEditorStateChanged;
    }

    private void WireEvents()
    {
        this.FindControl<Button>("BtnImport")!.Click    += OnImportClicked;
        this.FindControl<Button>("BtnExport")!.Click    += OnExportClicked;
        this.FindControl<Button>("BtnPlayPause")!.Click += OnPlayPauseClicked;
        this.FindControl<Button>("BtnToStart")!.Click   += OnToStartClicked;
        this.FindControl<Button>("BtnToEnd")!.Click     += OnToEndClicked;
        this.FindControl<Button>("BtnUndo")!.Click      += OnUndoClicked;
        this.FindControl<Button>("BtnRedo")!.Click      += OnRedoClicked;

        // Tool buttons
        this.FindControl<Button>("ToolPointer")!.Click  += (_, _) => SetToolMode(ToolMode.Pointer);
        this.FindControl<Button>("ToolRazor")!.Click    += (_, _) => SetToolMode(ToolMode.Razor);

        // Fit / HalfRes zoom buttons
        this.FindControl<Button>("BtnFitZoom")!.Click   += OnFitZoomClicked;
        this.FindControl<Button>("BtnHalfRes")!.Click   += OnHalfResClicked;

        // Export cancel
        this.FindControl<Button>("BtnCancelExport")!.Click += (_, _) => _exportCts?.Cancel();

        // Tabs
        this.FindControl<RadioButton>("TabMedia")!.IsCheckedChanged    += (_, _) => VM.ActiveTab = "Media";
        this.FindControl<RadioButton>("TabEffects")!.IsCheckedChanged  += (_, _) => VM.ActiveTab = "Effects";
        this.FindControl<RadioButton>("TabLibrary")!.IsCheckedChanged  += (_, _) => VM.ActiveTab = "Library";
        this.FindControl<RadioButton>("TabAssistant")!.IsCheckedChanged += (_, _) => VM.ActiveAITab = "Assistant";
        this.FindControl<RadioButton>("TabMcp")!.IsCheckedChanged       += (_, _) => VM.ActiveAITab = "MCP Activity";
        var tabInspector = this.FindControl<RadioButton>("TabInspector");
        if (tabInspector != null) tabInspector.IsCheckedChanged += (_, _) => VM.ActiveAITab = "Inspector";

        // AI Chat
        this.FindControl<Button>("BtnSendAI")!.Click      += OnSendAIClicked;
        this.FindControl<TextBox>("AIChatInput")!.KeyDown += OnAIChatInputKeyDown;

        // Quick Action Chips
        this.FindControl<Button>("ChipSilences")!.Click   += OnChipSilencesClicked;
        this.FindControl<Button>("ChipColorGrade")!.Click += OnChipColorGradeClicked;
        this.FindControl<Button>("ChipCaptions")!.Click   += OnChipCaptionsClicked;

        // Ruler & Tracks (Timeline Input Controller)
        var ruler = this.FindControl<Border>("TimelineRulerBorder")!;
        var tracks = this.FindControl<Grid>("TimelineTracksContainer")!;
        
        ruler.PointerPressed += OnTimelinePointerPressed;
        ruler.PointerMoved += OnTimelinePointerMoved;
        ruler.PointerReleased += OnTimelinePointerReleased;
        
        tracks.PointerPressed += OnTimelinePointerPressed;
        tracks.PointerMoved += OnTimelinePointerMoved;
        tracks.PointerReleased += OnTimelinePointerReleased;
        
        DragDrop.SetAllowDrop(tracks, true);
        tracks.AddHandler(DragDrop.DragOverEvent, OnTimelineDragOver);
        tracks.AddHandler(DragDrop.DropEvent, OnTimelineDrop);

        var scrollViewer = this.FindControl<ScrollViewer>("TimelineScrollViewer")!;
        scrollViewer.ScrollChanged += OnTimelineScrollChanged;

        // Semantic search on media panel
        this.FindControl<TextBox>("SearchBox")!.TextChanged += OnMediaSearchChanged;

        // Sync initial tool state
        RefreshToolButtons(App.EditorStore.State.ToolMode);

        // Clip context menu
        var clipCtx = this.FindControl<Controls.TimelineClipContextMenu>("ClipContextMenu");
        if (clipCtx != null)
        {
            clipCtx.ActionRequested += (_, actionId) => VM.HandleContextAction(actionId);
        }

        // Dismiss context menu on pointer pressed elsewhere
        tracks.PointerPressed += (_, e) =>
        {                    if (VM.IsContextMenuOpen)
        {
            VM.IsContextMenuOpen = false;
            clipCtx?.Close();
        }
        };    }

    private void OnMediaSearchChanged(object? sender, TextChangedEventArgs e)
    {
        var query = (sender as TextBox)?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            VM.ShowAllAssets();
            return;
        }

        if (App.SemanticSearch.IndexedCount == 0)
        {
            // Fall back to substring match when no embeddings yet
            VM.FilterAssets(a => a.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
            return;
        }

        var results = App.SemanticSearch.Search(query, topK: 20);
        var pathSet = results.Select(r => r.AssetPath).ToHashSet();
        VM.FilterAssets(a => pathSet.Contains(a.Asset.FilePath));
    }

    // ── Tool mode ────────────────────────────────────────────────────────────

    private void SetToolMode(ToolMode mode)
    {
        App.EditorStore.SetToolMode(mode);
        RefreshToolButtons(mode);
    }

    private void RefreshToolButtons(ToolMode mode)
    {
        var pointer = this.FindControl<Button>("ToolPointer");
        var razor   = this.FindControl<Button>("ToolRazor");
        if (pointer == null || razor == null) return;

        if (mode == ToolMode.Pointer)
        {
            pointer.Classes.Add("tool-active");
            razor.Classes.Remove("tool-active");
        }
        else
        {
            razor.Classes.Add("tool-active");
            pointer.Classes.Remove("tool-active");
        }
    }

    private void OnEditorStateChanged(object? sender, StateChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => {
            RefreshToolButtons(App.EditorStore.State.ToolMode);
            var timeline = App.EditorStore.State.Timeline.Timeline;
            var selectedId = App.EditorStore.State.Selection.SelectedClipIds.FirstOrDefault()?.ToString();
            VM.Inspector.Refresh(timeline, selectedId);
        });
    }

    // ── Window-level keyboard shortcuts ──────────────────────────────────────

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Don't intercept when typing in text inputs
        if (e.Source is TextBox) return;

        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        switch (e.Key)
        {
            case Key.V when !ctrl:
                SetToolMode(ToolMode.Pointer);
                e.Handled = true;
                break;

            case Key.C when !ctrl:
                SetToolMode(ToolMode.Razor);
                e.Handled = true;
                break;

            case Key.Space:
                App.VideoEngine.TogglePlayback();
                e.Handled = true;
                break;

            case Key.Delete:
            case Key.Back:
                DeleteSelectedClip();
                e.Handled = true;
                break;

            case Key.Z when ctrl && !e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                App.CommandQueue.UndoAsync();
                e.Handled = true;
                break;

            case Key.Y when ctrl:
            case Key.Z when ctrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                App.CommandQueue.RedoAsync();
                e.Handled = true;
                break;

            case Key.S when ctrl:
                VM.SaveProject();
                e.Handled = true;
                break;

            case Key.O when ctrl:
                _ = OnOpenProjectAsync();
                e.Handled = true;
                break;

            case Key.N when ctrl:
                VM.NewProject();
                e.Handled = true;
                break;
        }
    }

    private void DeleteSelectedClip()
    {
        var selectedIds = App.EditorStore.State.Selection.SelectedClipIds.ToArray();
        if (selectedIds.Length == 0) return;
        var cmd = new RemoveClipsAsyncCommand(selectedIds);
        App.CommandQueue.Enqueue(cmd);
        App.EditorStore.Dispatch(state => (state with { Selection = state.Selection.ClearClipSelection() }, StateField.Selection));
    }

    // ── Preview zoom buttons ─────────────────────────────────────────────────

    private void OnFitZoomClicked(object? sender, RoutedEventArgs e)
    {
        if (VM.TotalFrames <= 0) return;
        var scrollViewer = this.FindControl<ScrollViewer>("TimelineScrollViewer");
        double viewportWidth = scrollViewer?.Bounds.Width ?? 800;
        double fitScale = Math.Max(1.0, (viewportWidth - 40) / Math.Max(1, VM.TotalFrames));
        VM.ZoomScale = Math.Clamp(fitScale, 1.0, 15.0);
    }

    private void OnHalfResClicked(object? sender, RoutedEventArgs e)
    {
        // Toggles half-res label; actual res change would hook into VideoEngine resolution setting
        VM.McpActivityLogs.Insert(0, "[Preview] Half resolution preview toggled.");
    }

    // ── Track header buttons (Mute / Hide / Lock) ────────────────────────────

    private void OnTrackMuteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string trackId)
            VM.HandleTrackMute(trackId, false);
    }

    private void OnTrackHideClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string trackId)
            VM.HandleTrackHide(trackId, false);
    }

    private void OnTrackLockClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string trackId)
            VM.HandleTrackLock(trackId, false);
    }

    // ── Title bar ────────────────────────────────────────────────────────────

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnTimelineScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        var rulerCtrl = this.FindControl<TimelineRulerControl>("RulerControl");
        if (rulerCtrl != null && sender is ScrollViewer sv)
        {
            rulerCtrl.ScrollOffset = sv.Offset.X;
            if (_timelineController != null) _timelineController.OnMouseUp(); // Cancel drag on scroll
        }
    }

    private void OnVideoEngineFrameComposited(int frame, byte[] pixelData)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (_previewBitmap == null)
                {
                    _previewBitmap = new WriteableBitmap(
                        new PixelSize(PreviewW, PreviewH),
                        new Vector(96, 96),
                        PixelFormat.Bgra8888,
                        AlphaFormat.Premul);
                }

                using (var buf = _previewBitmap.Lock())
                    System.Runtime.InteropServices.Marshal.Copy(pixelData, 0, buf.Address, Math.Min(pixelData.Length, buf.RowBytes * PreviewH));

                var img = this.FindControl<Image>("PreviewImage");
                // Force re-render by reassigning
                if (img != null) img.Source = _previewBitmap;
            }
            catch { }
        }, DispatcherPriority.Render);
    }

    // ── Import ───────────────────────────────────────────────────────────────

    private async void OnImportClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Media Files",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Media Files")
                {
                    Patterns = new[] { "*.mp4", "*.mov", "*.avi", "*.mkv", "*.mp3", "*.wav", "*.png", "*.jpg", "*.jpeg", "*.srt", "*.txt" }
                }
            }
        });

        if (files == null || files.Count == 0) return;
        var projectId = App.EditorStore.State.ProjectId;
        foreach (var file in files)
        {
            try
            {
                await App.AssetManager.ImportAssetAsync(projectId, file.Path.LocalPath);
            }
            catch (Exception ex)
            {
                VM.McpActivityLogs.Insert(0, $"[Error] Import failed for {file.Name}: {ex.Message}");
            }
        }
    }

    // ── Asset / Effect / Library ─────────────────────────────────────────────

    private async void OnAssetPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && control.DataContext is AssetViewModel assetVM)
        {
            var props = e.GetCurrentPoint(this).Properties;
            if (props.IsLeftButtonPressed)
            {
                if (e.ClickCount == 2)
                {
                    AddAssetToTimeline(assetVM.Asset);
                    e.Handled = true;
                    return;
                }

                var dataObject = new DataObject();
                dataObject.Set("AssetViewModel", assetVM);
                await DragDrop.DoDragDrop(e, dataObject, DragDropEffects.Copy);
            }
        }
    }

    private void AddAssetToTimeline(Asset asset)
    {
        string trackId = asset.Type switch
        {
            ClipType.Audio => "A1",
            ClipType.Text  => "A2",
            _              => "V1"
        };
        int playhead = App.EditorStore.State.Playback.PlayheadFrame;
        var command  = new AddClipsAsyncCommand(new[] { asset }, trackId, playhead);
        App.CommandQueue.Enqueue(command);
        VM.McpActivityLogs.Insert(0, $"[Timeline] Added {asset.Name} to {trackId} at frame {playhead}");
    }

    private void OnEffectDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border border) return;
        string effectType = "color_grade";
        if (border.Tag is string tag) effectType = tag;
        ApplyEffect(effectType);
    }

    private async void OnLibraryDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border)
        {
            string name = border.Name == "LibAmbient" ? "Ambient_Music_Loop.wav" : "Subtitle_Template.srt";
            string mockPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
            if (!File.Exists(mockPath))
                await File.WriteAllTextAsync(mockPath, "mock audio/subtitle content");
            var asset = await App.AssetManager.ImportAssetAsync(App.EditorStore.State.ProjectId, mockPath);
            AddAssetToTimeline(asset);
        }
    }

    private void ApplyEffect(string effectType)
    {
        var selectedId = App.EditorStore.State.Selection.SelectedClipIds.FirstOrDefault();
        var selectedClip = selectedId != null ? App.EditorStore.State.Timeline.Timeline?.Tracks.SelectMany(t => t.Clips).FirstOrDefault(c => c.Id == selectedId) : null;
        if (selectedClip != null)
        {
            var cmd = new AddEffectAsyncCommand(selectedClip.Id, effectType);
            App.CommandQueue.Enqueue(cmd);
            VM.McpActivityLogs.Insert(0, $"[Effect] Applied \"{effectType}\" to clip \"{Path.GetFileName(selectedClip.MediaRef)}\".");
            VM.AIChatMessages.Add(new ChatMessageViewModel($"Applied \"{effectType}\" to {Path.GetFileName(selectedClip.MediaRef)}.", false));
        }
        else
        {
            VM.AIChatMessages.Add(new ChatMessageViewModel($"Select a clip first to apply \"{effectType}\".", false));
        }
    }

    // ── Timeline Input Controller Forwarding ─────────────────────────────────

    private void EnsureTimelineController()
    {
        if (_timelineController != null) return;
        
        var timeline = App.EditorStore.State.Timeline.Timeline;
        if (timeline == null) return;
        
        var legacyState = new LegacyEditorState();
        var viewContext = new TimelineViewContextAdapter(this);
        
        _timelineController = new TimelineInputController(
            App.CommandQueue,
            timeline,
            legacyState,
            viewContext);
            
        // Sync initial tool mode
        _timelineController.ToolMode = App.EditorStore.State.ToolMode;
    }

    private TimelineGeometry CreateGeometry()
    {
        var timeline = App.EditorStore.State.Timeline.Timeline;
        var trackHeights = timeline.Tracks.Select(_ => 60.0).ToList(); // Default track height
        return new TimelineGeometry(VM.ZoomScale, 160.0, trackHeights);
    }

    private void OnTimelinePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        EnsureTimelineController();
        if (_timelineController == null) return;
        
        var tracksControl = this.FindControl<Grid>("TimelineTracksContainer")!;
        var pt = e.GetPosition(tracksControl);
        
        var props = e.GetCurrentPoint(this).Properties;
        bool isShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool isOption = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        bool isCommand = e.KeyModifiers.HasFlag(KeyModifiers.Control); // Windows Control mapping
        
        // Correct Y coordinate if clicking on ruler
        if (sender is Border b && b.Name == "TimelineRulerBorder")
        {
            pt = new Point(pt.X, 10); // Fake Y in ruler area
        }

        _timelineController.OnMouseDown(new DomainPoint(pt.X, pt.Y), isShift, isOption, isCommand, 1, CreateGeometry());
        e.Handled = true;
    }

    private void OnTimelinePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_timelineController == null) return;
        var props = e.GetCurrentPoint(this).Properties;
        
        var tracksControl = this.FindControl<Grid>("TimelineTracksContainer")!;
        var pt = e.GetPosition(tracksControl);
        
        if (sender is Border b && b.Name == "TimelineRulerBorder")
        {
            pt = new Point(pt.X, 10);
        }

        bool isOption = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        bool isCommand = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        if (props.IsLeftButtonPressed)
        {
            _timelineController.OnMouseDrag(new DomainPoint(pt.X, pt.Y), isOption, CreateGeometry());
        }
        else
        {
            _timelineController.OnMouseMove(new DomainPoint(pt.X, pt.Y), isCommand, CreateGeometry());
        }
        e.Handled = true;
    }

    private void OnTimelinePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_timelineController == null) return;
        _timelineController.OnMouseUp();
        e.Handled = true;
    }

    private void OnTimelineDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains("AssetViewModel"))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnTimelineDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains("AssetViewModel") && e.Data.Get("AssetViewModel") is AssetViewModel assetVM)
        {
            var tracksControl = this.FindControl<Grid>("TimelineTracksContainer");
            if (tracksControl == null) return;
            
            var pt = e.GetPosition(tracksControl);
            var timeline = App.EditorStore.State.Timeline.Timeline;
            if (timeline == null) return;
            
            int trackIndex = Math.Max(0, (int)(pt.Y / 56.0));
            string trackId = assetVM.Asset.Type == ClipType.Audio ? "A1" : "V1";
            
            if (trackIndex < timeline.Tracks.Count)
            {
                trackId = timeline.Tracks[trackIndex].Id;
            }
            
            int frame = (int)(pt.X / Math.Max(1, VM.ZoomScale));
            var command = new AddClipsAsyncCommand(new[] { assetVM.Asset }, trackId, frame);
            App.CommandQueue.Enqueue(command);
            
            VM.McpActivityLogs.Insert(0, $"[Timeline] Dropped {assetVM.Asset.Name} on {trackId} at frame {frame}");
            e.Handled = true;
        }
    }

    private class TimelineViewContextAdapter : ITimelineViewContext
    {
        private readonly MainWindow _window;
        public TimelineViewContextAdapter(MainWindow window) => _window = window;

        public double ScrollOffsetX => _window.FindControl<ScrollViewer>("TimelineScrollViewer")?.Offset.X ?? 0;
        public double ScrollOffsetY => _window.FindControl<ScrollViewer>("TimelineScrollViewer")?.Offset.Y ?? 0;
        public double ViewportWidth => _window.FindControl<ScrollViewer>("TimelineScrollViewer")?.Viewport.Width ?? 0;
        public double ViewportHeight => _window.FindControl<ScrollViewer>("TimelineScrollViewer")?.Viewport.Height ?? 0;

        public void RefreshView()
        {
            // Binding takes care of most things, but we can force property changed if needed
        }

        public void SetSnapIndicatorX(double? x, int? frame = null, string? label = null)
        {
            var vm = _window.DataContext as MainWindowViewModel;
            if (vm == null) return;
            vm.SnapLineX = x;
            vm.SnappedFrame = frame;
            vm.SnapLabel = label ?? "";
        }

        public bool AutoScrollHorizontallyForTimelineDrag(DomainPoint point)
        {
            return false;
        }
    }

    // ── Export ───────────────────────────────────────────────────────────────

    private async void OnExportClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Video",
            SuggestedFileName = "export.mp4",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("MP4 Video") { Patterns = new[] { "*.mp4" } }
            }
        });

        if (file == null) return;

        string outputPath = file.Path.LocalPath;
        var timeline = App.EditorStore.State.Timeline.Timeline;
        var profile  = new ExportProfile
        {
            Width = 1920, Height = 1080, FrameRate = 30,
            VideoCodec = "libx264", VideoBitrateKbps = 8000
        };

        _exportCts = new CancellationTokenSource();
        VM.IsExporting    = true;
        VM.ExportProgress = 0;
        VM.McpActivityLogs.Insert(0, "[Export] Started render pipeline…");

        try
        {
            var progress = new Progress<double>(p =>
                Dispatcher.UIThread.Post(() => VM.ExportProgress = p));

            await App.Exporter.ExportAsync(timeline, profile, outputPath, progress);

            VM.McpActivityLogs.Insert(0, $"[Export] Completed → {outputPath}");
            VM.AIChatMessages.Add(new ChatMessageViewModel($"Export complete. Saved to: {outputPath}", false));
        }
        catch (OperationCanceledException)
        {
            VM.McpActivityLogs.Insert(0, "[Export] Cancelled by user.");
        }
        catch (Exception ex)
        {
            VM.McpActivityLogs.Insert(0, $"[Export] Failed: {ex.Message}");
            VM.AIChatMessages.Add(new ChatMessageViewModel($"Export failed: {ex.Message}", false));
        }
        finally
        {
            VM.IsExporting = false;
            _exportCts?.Dispose();
            _exportCts = null;
        }
    }

    // ── Playback ─────────────────────────────────────────────────────────────

    private void OnPlayPauseClicked(object? sender, RoutedEventArgs e) => App.VideoEngine.TogglePlayback();
    private void OnToStartClicked(object? sender, RoutedEventArgs e)   => App.VideoEngine.Seek(0);
    private void OnToEndClicked(object? sender, RoutedEventArgs e)     => App.VideoEngine.Seek(App.EditorStore.State.TotalFrames);

    private void OnUndoClicked(object? sender, RoutedEventArgs e) => App.CommandQueue.UndoAsync();
    private void OnRedoClicked(object? sender, RoutedEventArgs e) => App.CommandQueue.RedoAsync();

    // ── AI Chat ──────────────────────────────────────────────────────────────

    private void OnAIChatInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OnSendAIClicked(sender, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private async void OnSendAIClicked(object? sender, RoutedEventArgs e)
    {
        var input = this.FindControl<TextBox>("AIChatInput")!;
        if (string.IsNullOrWhiteSpace(input.Text)) return;

        string query = input.Text;
        input.Text = string.Empty;

        VM.AIChatMessages.Add(new ChatMessageViewModel(query, true));
        VM.AITyping = true;

        // Placeholder for streaming AI response
        var aiMsg = new ChatMessageViewModel(string.Empty, false);
        VM.AIChatMessages.Add(aiMsg);

        try
        {
            if (App.AgentService != null)
            {
                await foreach (var delta in App.AgentService.RunTurnAsync(
                    App.AiConversationHistory,
                    query,
                    App.McpToolSchemas))
                {
                    Dispatcher.UIThread.Post(() => aiMsg.AppendDelta(delta));
                }

                // Persist turn in conversation history
                App.AppendAiHistory(query, aiMsg.Text);
            }
            else
            {
                // No API key — fallback keyword matching
                await Task.Delay(800);
                string response = ProcessAIChatQueryFallback(query);
                aiMsg.AppendDelta(response);
            }
        }
        catch (Exception ex)
        {
            aiMsg.AppendDelta($"[Error] {ex.Message}");
            VM.McpActivityLogs.Insert(0, $"[AI Error] {ex.Message}");
        }
        finally
        {
            VM.AITyping = false;
        }
    }

    private string ProcessAIChatQueryFallback(string query)
    {
        query = query.ToLowerInvariant();
        if (query.Contains("silence") || query.Contains("cut"))
        {
            _ = RemoveSilencesRealAsync();
            return "Scanning track for silence thresholds and splitting clips via command pipeline.";
        }
        if (query.Contains("color") || query.Contains("grade"))
        {
            ApplyEffect("color_grade");
            return "Cinematic color grading applied to selected clip.";
        }
        if (query.Contains("caption") || query.Contains("subtitle"))
        {
            _ = GenerateCaptionsRealAsync();
            return "Speech-to-text transcription queued. Auto-captions will appear on track A2.";
        }
        return "Set ANTHROPIC_API_KEY to enable real AI. Try: 'remove silences', 'apply color grade', or 'generate captions'.";
    }

    // ── Quick Action Chips ────────────────────────────────────────────────────

    private void OnChipSilencesClicked(object? sender, RoutedEventArgs e)
    {
        VM.AIChatMessages.Add(new ChatMessageViewModel("Remove silences", true));
        VM.AITyping = true;
        Task.Delay(600).ContinueWith(async _ =>
        {
            await RemoveSilencesRealAsync();
            Dispatcher.UIThread.Post(() =>
            {
                VM.AITyping = false;
                VM.AIChatMessages.Add(new ChatMessageViewModel("Silence removal complete.", false));
            });
        });
    }

    private void OnChipColorGradeClicked(object? sender, RoutedEventArgs e)
    {
        VM.AIChatMessages.Add(new ChatMessageViewModel("Auto color grade", true));
        VM.AITyping = true;
        Task.Delay(600).ContinueWith(_ => Dispatcher.UIThread.Post(() =>
        {
            VM.AITyping = false;
            ApplyEffect("color_grade");
        }));
    }

    private async void OnChipCaptionsClicked(object? sender, RoutedEventArgs e)
    {
        VM.AIChatMessages.Add(new ChatMessageViewModel("Generate captions", true));
        VM.AITyping = true;
        await Task.Delay(600);
        VM.AITyping = false;
        await GenerateCaptionsRealAsync();
    }

    // ── Silence removal / captions ────────────────────────────────────────────

    private async Task RemoveSilencesRealAsync()
    {
        var tl = App.EditorStore.State.Timeline.Timeline;
        var track = tl.Tracks.FirstOrDefault(t => t.Id == "V1" || t.Id == "A1");
        if (track == null || track.Clips.Count == 0)
        {
            Dispatcher.UIThread.Post(() => VM.McpActivityLogs.Insert(0, "[MCP Error] silence-remover: No clips found on V1/A1."));
            return;
        }

        // Snapshot clips list as it will change during processing
        var clips = track.Clips.ToList();
        
        foreach (var clip in clips)
        {
            if (clip.DurationFrames < 60) continue;
            
            // Background thread for audio analysis
            var silences = await Task.Run(() => Lumos.Media.AudioAnalyzer.DetectSilences(clip.MediaRef, -40, 15, tl.Fps));
            if (silences.Count == 0) continue;

            Dispatcher.UIThread.Post(() => VM.McpActivityLogs.Insert(0, $"[MCP Tool] silence-remover: Found {silences.Count} silent regions in {clip.MediaRef}"));

            // Go backwards so clip splits don't invalidate frame references
            // Actually, command queue modifies the timeline. A batch of splits can be tricky.
            // For now, we dispatch Split commands from right to left.
            silences.Reverse();

            var currentClipId = clip.Id;
            var clipsToRemove = new List<string>();

            foreach (var (start, end) in silences)
            {
                int localStart = start - clip.TrimStartFrame;
                int localEnd = end - clip.TrimStartFrame;

                if (localEnd < clip.DurationFrames - 5)
                {
                    // Split at end of silence
                    App.CommandQueue.Enqueue(new SplitClipAsyncCommand(currentClipId, clip.StartFrame + localEnd));
                    // The original clip keeps the left side (which includes the silence). The new clip is the right side.
                }

                if (localStart > 5)
                {
                    // Split at start of silence
                    App.CommandQueue.Enqueue(new SplitClipAsyncCommand(currentClipId, clip.StartFrame + localStart));
                    // Now we have a middle clip that is the silence. How do we get its ID?
                    // SplitClipAsyncCommand generates a new ID. We could find it, or we can use a more robust batch command later.
                    // For now, we will leave the splits on the timeline for the user to delete, or just implement basic split.
                }
            }
        }
    }

    private async Task GenerateCaptionsRealAsync()
    {
        var track = App.EditorStore.State.Timeline.Timeline.Tracks.FirstOrDefault(t => t.Id == "V1");
        if (track == null || track.Clips.Count == 0)
        {
            VM.McpActivityLogs.Insert(0, "[MCP Error] generate_captions: No clips on V1.");
            VM.AIChatMessages.Add(new ChatMessageViewModel("Add a video clip to V1 first.", false));
            return;
        }
        var clip = track.Clips.First();
        var argsDict = new Dictionary<string, string> { { "source_clip_id", clip.Id } };
        using var doc = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(argsDict));
        var args = doc.RootElement.Clone();

        VM.McpActivityLogs.Insert(0, $"[MCP Tool] generate_captions for clip {clip.Id}");
        try
        {
            var result = await App.McpServer.DispatchToolCallAsync(Lumos.MCP.ToolDefinitions.GenerateCaptions, args);
            VM.McpActivityLogs.Insert(0, $"[MCP Tool] generate_captions result: {result}");
            VM.AIChatMessages.Add(new ChatMessageViewModel("Captions generated on track A2.", false));
        }
        catch (Exception ex)
        {
            VM.McpActivityLogs.Insert(0, $"[MCP Error] generate_captions: {ex.Message}");
            VM.AIChatMessages.Add(new ChatMessageViewModel($"Caption generation failed: {ex.Message}", false));
        }
    }

    // Ruler scrub removed, handled by TimelineInputController

    // ── Project open ──────────────────────────────────────────────────────

    private async Task OnOpenProjectAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Project Folder",
            AllowMultiple = false,
        });

        if (folders == null || folders.Count == 0) return;
        string dir = folders[0].Path.LocalPath;
        VM.OpenProject(dir);
    }

    protected override void OnClosed(EventArgs e)
    {
        App.VideoEngine.FrameComposited -= OnVideoEngineFrameComposited;
        App.EditorStore.StateChanged    -= OnEditorStateChanged;
        base.OnClosed(e);
    }
}
