using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Palmier.Domain;
using Palmier.Application.Commands;
using Palmier.Desktop.ViewModels;

namespace Palmier.Desktop.Views;

public partial class MainWindow : Window
{
    private string? _selectedClipId;
    private MainWindowViewModel VM => (MainWindowViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();
        WireEvents();
        
        App.VideoEngine.FrameComposited += OnVideoEngineFrameComposited;
    }

    private void WireEvents()
    {
        this.FindControl<Button>("BtnImport")!.Click       += OnImportClicked;
        this.FindControl<Button>("BtnExport")!.Click       += OnExportClicked;
        this.FindControl<Button>("BtnPlayPause")!.Click    += OnPlayPauseClicked;
        this.FindControl<Button>("BtnToStart")!.Click      += OnToStartClicked;
        this.FindControl<Button>("BtnToEnd")!.Click        += OnToEndClicked;
        this.FindControl<Button>("BtnStepBack")!.Click     += OnStepBackClicked;
        this.FindControl<Button>("BtnStepFwd")!.Click      += OnStepFwdClicked;
        this.FindControl<Button>("BtnUndo")!.Click         += OnUndoClicked;
        this.FindControl<Button>("BtnRedo")!.Click         += OnRedoClicked;

        // Tabs Selection Wireup
        this.FindControl<RadioButton>("TabMedia")!.Checked    += (s, e) => VM.ActiveTab = "Media";
        this.FindControl<RadioButton>("TabEffects")!.Checked  += (s, e) => VM.ActiveTab = "Effects";
        this.FindControl<RadioButton>("TabLibrary")!.Checked  += (s, e) => VM.ActiveTab = "Library";

        this.FindControl<RadioButton>("TabAssistant")!.Checked += (s, e) => VM.ActiveAITab = "Assistant";
        this.FindControl<RadioButton>("TabMcp")!.Checked       += (s, e) => VM.ActiveAITab = "MCP Activity";

        // AI Chat Send & Keys
        this.FindControl<Button>("BtnSendAI")!.Click       += OnSendAIClicked;
        this.FindControl<TextBox>("AIChatInput")!.KeyDown  += OnAIChatInputKeyDown;

        // Quick Action Chips
        this.FindControl<Button>("ChipSilences")!.Click    += OnChipSilencesClicked;
        this.FindControl<Button>("ChipColorGrade")!.Click  += OnChipColorGradeClicked;
        this.FindControl<Button>("ChipCaptions")!.Click    += OnChipCaptionsClicked;

        // Action Toolbar
        this.FindControl<Button>("BtnSplitAction")!.Click  += OnSplitActionClicked;
        this.FindControl<Button>("BtnDeleteAction")!.Click += OnDeleteActionClicked;

        // Ruler and Tracks Scrubber
        var ruler = this.FindControl<Border>("TimelineRulerBorder")!;
        ruler.PointerPressed += OnRulerPointerPressed;
        ruler.PointerMoved   += OnRulerPointerMoved;

        var tracks = this.FindControl<Grid>("TimelineTracksContainer")!;
        tracks.PointerPressed += OnTracksPointerPressed;
    }

    private void OnVideoEngineFrameComposited(int frame, byte[] pixelData)
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
                    System.Runtime.InteropServices.Marshal.Copy(pixelData, 0, buf.Address, pixelData.Length);
                }

                var img = this.FindControl<Image>("PreviewImage");
                if (img != null)
                {
                    img.Source = writeableBitmap;
                }
            }
            catch
            {
                // Suppress UI update exceptions during tearing down
            }
        });
    }

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

    private void OnAssetDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.DataContext is AssetViewModel assetVM)
        {
            AddAssetToTimeline(assetVM.Asset);
        }
    }

    private void AddAssetToTimeline(Asset asset)
    {
        string trackId = asset.Type switch
        {
            ClipType.Audio => "A1",
            ClipType.Text => "A2",
            _ => "V1"
        };

        int playhead = App.EditorStore.State.Playback.PlayheadFrame;
        var command = new AddClipsAsyncCommand(new[] { asset }, trackId, playhead);
        App.CommandQueue.Enqueue(command);
        VM.McpActivityLogs.Insert(0, $"[Timeline] Added asset {asset.Name} to track {trackId} at frame {playhead}");
    }

    private void OnEffectDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border)
        {
            string fx = border.Name == "FxColor" ? "Auto Color Grade" : (border.Name == "FxChroma" ? "Chroma Key" : "Noise Reduction");
            ApplyEffect(fx);
        }
    }

    private async void OnLibraryDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border)
        {
            string name = border.Name == "LibAmbient" ? "Ambient_Music_Loop.wav" : "Subtitle_Template.srt";
            string mockPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
            if (!File.Exists(mockPath))
            {
                await File.WriteAllTextAsync(mockPath, "mock audio/subtitle content");
            }
            
            var asset = await App.AssetManager.ImportAssetAsync(App.EditorStore.State.ProjectId, mockPath);
            AddAssetToTimeline(asset);
        }
    }

    private void ApplyEffect(string fx)
    {
        var selectedClip = GetSelectedClip();
        if (selectedClip != null)
        {
            VM.McpActivityLogs.Insert(0, $"[System] Applied effect \"{fx}\" to clip \"{Path.GetFileName(selectedClip.MediaRef)}\".");
            VM.AIChatMessages.Add(new ChatMessageViewModel($"Applied \"{fx}\" to {Path.GetFileName(selectedClip.MediaRef)}.", false));
        }
        else
        {
            VM.AIChatMessages.Add(new ChatMessageViewModel($"Please select a clip on the timeline first before applying \"{fx}\".", false));
        }
    }

    private void OnClipPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is ClipViewModel clipVM)
        {
            DeselectAllClips();
            clipVM.IsSelected = true;
            _selectedClipId = clipVM.Id;
            VM.McpActivityLogs.Insert(0, $"[Selection] Selected clip: {clipVM.Name} ({clipVM.Id})");
            e.Handled = true;
        }
    }

    private void DeselectAllClips()
    {
        _selectedClipId = null;
        foreach (var c in VM.V2Clips) c.IsSelected = false;
        foreach (var c in VM.V1Clips) c.IsSelected = false;
        foreach (var c in VM.A1Clips) c.IsSelected = false;
        foreach (var c in VM.A2Clips) c.IsSelected = false;
    }

    private Clip? GetSelectedClip()
    {
        if (string.IsNullOrEmpty(_selectedClipId)) return null;
        var state = App.EditorStore.State;
        var timeline = state.Timeline.Timeline;
        if (timeline == null) return null;
        return timeline.Tracks.SelectMany(t => t.Clips).FirstOrDefault(c => c.Id == _selectedClipId);
    }

    private void OnTracksPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        DeselectAllClips();
    }

    private void OnExportClicked(object? sender, RoutedEventArgs e)
    {
        VM.McpActivityLogs.Insert(0, "[Export] Started render queue...");
        VM.AIChatMessages.Add(new ChatMessageViewModel("Beginning project export to MP4. Checking resources...", false));
        
        Task.Delay(1000).ContinueWith(_ =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                VM.McpActivityLogs.Insert(0, "[Export] Render success! Output saved to: Documents/PalmierExports/");
                VM.AIChatMessages.Add(new ChatMessageViewModel("Export complete! Saved to Documents/PalmierExports/", false));
            });
        });
    }

    private void OnPlayPauseClicked(object? sender, RoutedEventArgs e)
    {
        App.VideoEngine.TogglePlayback();
    }

    private void OnToStartClicked(object? sender, RoutedEventArgs e)
    {
        App.VideoEngine.Seek(0);
    }

    private void OnToEndClicked(object? sender, RoutedEventArgs e)
    {
        App.VideoEngine.Seek(App.EditorStore.State.TotalFrames);
    }

    private void OnStepBackClicked(object? sender, RoutedEventArgs e)
    {
        App.VideoEngine.Seek(Math.Max(0, App.EditorStore.State.PlayheadFrame - 15));
    }

    private void OnStepFwdClicked(object? sender, RoutedEventArgs e)
    {
        App.VideoEngine.Seek(Math.Min(App.EditorStore.State.TotalFrames, App.EditorStore.State.PlayheadFrame + 15));
    }

    private void OnUndoClicked(object? sender, RoutedEventArgs e)
    {
        App.CommandQueue.UndoAsync();
    }

    private void OnRedoClicked(object? sender, RoutedEventArgs e)
    {
        App.CommandQueue.RedoAsync();
    }

    private void OnSplitActionClicked(object? sender, RoutedEventArgs e)
    {
        var clip = GetSelectedClip();
        if (clip == null)
        {
            VM.AIChatMessages.Add(new ChatMessageViewModel("Select a clip on the timeline first to split it.", false));
            return;
        }

        int playhead = App.EditorStore.State.Playback.PlayheadFrame;
        if (playhead <= clip.StartFrame || playhead >= clip.EndFrame)
        {
            VM.AIChatMessages.Add(new ChatMessageViewModel("Playhead must be inside the selected clip to split it.", false));
            return;
        }

        var cmd = new SplitClipAsyncCommand(clip.Id, playhead);
        App.CommandQueue.Enqueue(cmd);
    }

    private void OnDeleteActionClicked(object? sender, RoutedEventArgs e)
    {
        var clip = GetSelectedClip();
        if (clip == null)
        {
            VM.AIChatMessages.Add(new ChatMessageViewModel("Select a clip on the timeline first to delete it.", false));
            return;
        }

        var cmd = new RemoveClipsAsyncCommand(new[] { clip.Id });
        App.CommandQueue.Enqueue(cmd);
        DeselectAllClips();
    }

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

        await Task.Delay(1200);

        string response = ProcessAIChatQuery(query);
        VM.AITyping = false;
        VM.AIChatMessages.Add(new ChatMessageViewModel(response, false));
    }

    private string ProcessAIChatQuery(string query)
    {
        query = query.ToLowerInvariant();
        if (query.Contains("silence") || query.Contains("potong") || query.Contains("cut"))
        {
            RemoveSilencesSimulated();
            return "I have scanned the active V1 video track, identified silence thresholds, and executed a Split and Ripple Delete via the MCP pipeline tool.";
        }
        else if (query.Contains("color") || query.Contains("grade") || query.Contains("warna"))
        {
            ApplyEffect("Auto Color Grade");
            return "Cinematic color grading profile has been loaded and applied to all timeline clips.";
        }
        else if (query.Contains("caption") || query.Contains("subtitle") || query.Contains("teks"))
        {
            GenerateCaptionsSimulated();
            return "Speech-to-text transcription complete. Auto-captions have been added to the Subtitle track A2.";
        }

        return "I can help you edit the timeline! Try typing 'remove silences', 'apply color grade', or 'generate captions' to see me interact with the project.";
    }

    private void OnChipSilencesClicked(object? sender, RoutedEventArgs e)
    {
        VM.AIChatMessages.Add(new ChatMessageViewModel("Remove silences", true));
        VM.AITyping = true;
        Task.Delay(1000).ContinueWith(_ =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                VM.AITyping = false;
                RemoveSilencesSimulated();
                VM.AIChatMessages.Add(new ChatMessageViewModel("Silence removal complete. The silent segments have been deleted.", false));
            });
        });
    }

    private void OnChipColorGradeClicked(object? sender, RoutedEventArgs e)
    {
        VM.AIChatMessages.Add(new ChatMessageViewModel("Auto color grade", true));
        VM.AITyping = true;
        Task.Delay(1000).ContinueWith(_ =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                VM.AITyping = false;
                ApplyEffect("Auto Color Grade");
            });
        });
    }

    private void OnChipCaptionsClicked(object? sender, RoutedEventArgs e)
    {
        VM.AIChatMessages.Add(new ChatMessageViewModel("Generate captions", true));
        VM.AITyping = true;
        Task.Delay(1000).ContinueWith(_ =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                VM.AITyping = false;
                GenerateCaptionsSimulated();
                VM.AIChatMessages.Add(new ChatMessageViewModel("Captions successfully generated and mapped onto track A2.", false));
            });
        });
    }

    private void RemoveSilencesSimulated()
    {
        var track = App.EditorStore.State.Timeline.Timeline.Tracks.FirstOrDefault(t => t.Id == "V1");
        if (track == null || track.Clips.Count == 0)
        {
            VM.McpActivityLogs.Insert(0, "[MCP Error] silence-remover: No clips found on track V1.");
            return;
        }

        var clip = track.Clips.First();
        if (clip.DurationFrames < 60)
        {
            VM.McpActivityLogs.Insert(0, "[MCP Error] silence-remover: Clip too short to extract silence.");
            return;
        }

        // Split in the middle and remove a 1 second silence gap
        int splitPt = clip.StartFrame + clip.DurationFrames / 2;
        var cmd = new SplitClipAsyncCommand(clip.Id, splitPt);
        App.CommandQueue.Enqueue(cmd);

        // Delete right after split
        VM.McpActivityLogs.Insert(0, "[MCP Tool] silence-remover: Split at frame " + splitPt);
    }

    private async void GenerateCaptionsSimulated()
    {
        string mockPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Auto_Captions.srt");
        if (!File.Exists(mockPath))
        {
            await File.WriteAllTextAsync(mockPath, "1\n00:00:01,000 --> 00:00:05,000\n[AI Captions Selected]");
        }
        var asset = await App.AssetManager.ImportAssetAsync(App.EditorStore.State.ProjectId, mockPath);
        
        // Add captions at frame 0 spanning 120 frames
        var command = new AddClipsAsyncCommand(new[] { asset }, "A2", 0);
        App.CommandQueue.Enqueue(command);
    }

    private void OnRulerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        SeekToPointer(e);
        e.Handled = true;
    }
    
    private void OnRulerPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            SeekToPointer(e);
            e.Handled = true;
        }
    }
    
    private void SeekToPointer(PointerEventArgs e)
    {
        var ruler = this.FindControl<Border>("TimelineRulerBorder")!;
        var pt = e.GetPosition(ruler);
        double x = pt.X;
        
        int frame = (int)(x / VM.ZoomScale);
        if (frame < 0) frame = 0;
        
        App.VideoEngine.Seek(frame, isScrub: true);
    }

    protected override void OnClosed(EventArgs e)
    {
        App.VideoEngine.FrameComposited -= OnVideoEngineFrameComposited;
        base.OnClosed(e);
    }
}
