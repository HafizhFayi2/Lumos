using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Lumos.Domain;
using Lumos.Application.Assets;
using Lumos.Application.State;
using Lumos.Application.Commands;

namespace Lumos.Desktop.ViewModels;

public record EffectItemViewModel(string DisplayName, string EffectId, string Icon);

public class MainWindowViewModel : ViewModelBase
{
    private string _projectName = "Untitled Project";
    public string ProjectName
    {
        get => _projectName;
        set => SetProperty(ref _projectName, value);
    }

    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        set => SetProperty(ref _isDirty, value);
    }

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                OnPropertyChanged(nameof(PlayPauseIcon));
            }
        }
    }

    public string PlayPauseIcon => IsPlaying ? "⏸" : "▶";

    private int _playheadFrame;
    public int PlayheadFrame
    {
        get => _playheadFrame;
        set
        {
            if (SetProperty(ref _playheadFrame, value))
            {
                OnPropertyChanged(nameof(PlayheadLeft));
                OnPropertyChanged(nameof(PlayheadHandleLeft));
                OnPropertyChanged(nameof(TimeCode));
            }
        }
    }

    private double _zoomScale = 4.0;
    public double ZoomScale
    {
        get => _zoomScale;
        set
        {
            if (SetProperty(ref _zoomScale, value))
            {
                OnPropertyChanged(nameof(PlayheadLeft));
                OnPropertyChanged(nameof(PlayheadHandleLeft));
                OnPropertyChanged(nameof(TimelineWidthPixels));
                foreach (var track in Tracks)
                {
                    foreach (var clip in track.Clips)
                    {
                        clip.UpdateZoom(value);
                    }
                }
            }
        }
    }

    private int _fps = 30;
    public int Fps
    {
        get => _fps;
        set => SetProperty(ref _fps, value);
    }

    public double PlayheadLeft => PlayheadFrame * ZoomScale;
    public double PlayheadHandleLeft => PlayheadLeft - 5;

    public string TimeCode
    {
        get
        {
            int fps = 30;
            int hours = PlayheadFrame / (fps * 3600);
            int minutes = (PlayheadFrame / (fps * 60)) % 60;
            int seconds = (PlayheadFrame / fps) % 60;
            int frames = PlayheadFrame % fps;
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}:{frames:D2}";
        }
    }

    public double TimelineWidthPixels => Math.Max(1200, TotalFrames * ZoomScale);

    private int _totalFrames;
    public int TotalFrames
    {
        get => _totalFrames;
        set
        {
            if (SetProperty(ref _totalFrames, value))
            {
                OnPropertyChanged(nameof(TimelineWidthPixels));
            }
        }
    }

    private bool _hasClips;
    public bool HasClips
    {
        get => _hasClips;
        set => SetProperty(ref _hasClips, value);
    }

    private bool _hasAssets;
    public bool HasAssets
    {
        get => _hasAssets;
        set => SetProperty(ref _hasAssets, value);
    }

    // Media library tabs
    private string _activeTab = "Media";
    public string ActiveTab
    {
        get => _activeTab;
        set
        {
            if (SetProperty(ref _activeTab, value))
            {
                OnPropertyChanged(nameof(IsMediaTabActive));
                OnPropertyChanged(nameof(IsEffectsTabActive));
                OnPropertyChanged(nameof(IsLibraryTabActive));
            }
        }
    }

    public bool IsMediaTabActive => ActiveTab == "Media";
    public bool IsEffectsTabActive => ActiveTab == "Effects";
    public bool IsLibraryTabActive => ActiveTab == "Library";

    // AI tabs
    private string _activeAITab = "Inspector";
    public string ActiveAITab
    {
        get => _activeAITab;
        set
        {
            if (_activeAITab != value)
            {
                _activeAITab = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsAssistantTabActive));
                OnPropertyChanged(nameof(IsMcpTabActive));
                OnPropertyChanged(nameof(IsInspectorTabActive));
            }
        }
    }

    public bool IsAssistantTabActive => ActiveAITab == "Assistant";
    public bool IsMcpTabActive => ActiveAITab == "MCP Activity";
    public bool IsInspectorTabActive => ActiveAITab == "Inspector";

    public ObservableCollection<AssetViewModel> Assets { get; } = new();
    public ObservableCollection<TrackViewModel> Tracks { get; } = new();
    
    public InspectorViewModel Inspector { get; } = new();

    public ObservableCollection<EffectItemViewModel> AvailableEffects { get; } = new()
    {
        new("Color Grade", "color_grade", "🎨"),
        new("Chroma Key", "chroma_key", "🔳"),
        new("Clarity", "clarity", "✨"),
        new("Glow", "glow", "🌟"),
        new("Grain", "grain", "📺"),
        new("Grade Curves", "grade_curves", "📉"),
        new("Highlights & Shadows", "highlights_shadows", "🌗"),
        new("LUT (Tetra)", "lut_tetra", "🎞"),
        new("Levels", "levels", "📊"),
        new("Color Wheels", "color_wheels", "🎡"),
        new("Vignette", "vignette", "🌑")
    };

    public ObservableCollection<ChatMessageViewModel> AIChatMessages { get; } = new();
    public ObservableCollection<string> McpActivityLogs { get; } = new();

    private ToolMode _activeToolMode = ToolMode.Pointer;
    public ToolMode ActiveToolMode
    {
        get => _activeToolMode;
        set
        {
            if (SetProperty(ref _activeToolMode, value))
            {
                OnPropertyChanged(nameof(IsPointerActive));
                OnPropertyChanged(nameof(IsRazorActive));
            }
        }
    }

    public bool IsPointerActive => ActiveToolMode == ToolMode.Pointer;
    public bool IsRazorActive   => ActiveToolMode == ToolMode.Razor;

    private bool _isExporting;
    public bool IsExporting
    {
        get => _isExporting;
        set => SetProperty(ref _isExporting, value);
    }

    private double _exportProgress;
    public double ExportProgress
    {
        get => _exportProgress;
        set => SetProperty(ref _exportProgress, value);
    }

    private bool _aiTyping;
    public bool AITyping
    {
        get => _aiTyping;
        set => SetProperty(ref _aiTyping, value);
    }

    public MainWindowViewModel()
    {
        // Add initial system message from Assistant
        AIChatMessages.Add(new ChatMessageViewModel("Hi! I'm Lumos AI, your creative co-editor. Tell me what you'd like to do, or select a quick action below.", false));
        McpActivityLogs.Add("[System] MCP server initialized and ready.");

        // Register to EditorStore events
        App.EditorStore.StateChanged += OnStateChanged;
        App.AssetManager.AssetAdded += OnAssetAdded;
        App.AssetManager.AssetRemoved += OnAssetRemoved;
        App.CommandQueue.CommandCompleted += OnCommandCompleted;

        // Load initial state
        LoadState(App.EditorStore.State);
    }

    private void OnStateChanged(object? sender, StateChangedEventArgs e)
    {
        LoadState(App.EditorStore.State);
        ActiveToolMode = App.EditorStore.State.ToolMode;
    }

    private void OnAssetAdded(object? sender, AssetEventArgs e)
    {
        App.VideoEngine.Rebuild();
        RefreshAssets();
    }

    private void OnAssetRemoved(object? sender, AssetEventArgs e)
    {
        App.VideoEngine.Rebuild();
        RefreshAssets();
    }

    private void OnCommandCompleted(object? sender, CommandCompletedEventArgs e)
    {
        string logMsg = $"[{DateTime.Now:HH:mm:ss}] CommandExecuted: \"{e.Command.Label}\" -> Succeeded: {e.Result.Succeeded}";
        McpActivityLogs.Insert(0, logMsg);
        App.VideoEngine.Rebuild();
    }

    private void RefreshAssets()
    {
        Assets.Clear();
        foreach (var asset in App.AssetManager.Catalog.GetAll())
        {
            Assets.Add(new AssetViewModel(asset));
        }
        HasAssets = Assets.Count > 0;
    }

    public void ShowAllAssets() => RefreshAssets();

    public void FilterAssets(Func<AssetViewModel, bool> predicate)
    {
        Assets.Clear();
        foreach (var asset in App.AssetManager.Catalog.GetAll())
        {
            var vm = new AssetViewModel(asset);
            if (predicate(vm))
                Assets.Add(vm);
        }
        HasAssets = Assets.Count > 0;
    }

    private void LoadState(EditorState state)
    {
        ProjectName = state.ProjectName;
        IsDirty = state.IsDirty;
        PlayheadFrame = state.Playback.PlayheadFrame;
        IsPlaying = state.Playback.IsPlaying;

        var timeline = state.Timeline.Timeline;
        if (timeline != null)
        {
            Fps = timeline.Fps > 0 ? timeline.Fps : 30;
            TotalFrames = timeline.TotalFrames;
            SyncTracks(timeline);
            HasClips = Tracks.Any(t => t.Clips.Any());
        }
    }

    private void SyncTracks(Timeline timeline)
    {
        var existingIds = Tracks.Select(t => t.Id).ToHashSet();
        var currentIds = timeline.Tracks.Select(t => t.Id).ToHashSet();

        // Remove deleted tracks
        for (int i = Tracks.Count - 1; i >= 0; i--)
        {
            if (!currentIds.Contains(Tracks[i].Id))
                Tracks.RemoveAt(i);
        }

        // Add or update tracks (maintain order from domain timeline)
        for (int i = 0; i < timeline.Tracks.Count; i++)
        {
            var coreTrack = timeline.Tracks[i];
            var existingTrack = Tracks.FirstOrDefault(t => t.Id == coreTrack.Id);
            
            if (existingTrack == null)
            {
                existingTrack = new TrackViewModel(coreTrack);
                Tracks.Insert(i, existingTrack);
            }
            else
            {
                // Ensure correct order if tracks were reordered
                int currentIndex = Tracks.IndexOf(existingTrack);
                if (currentIndex != i)
                {
                    Tracks.Move(currentIndex, i);
                }
            }

            existingTrack.SyncClips(coreTrack.Clips, ZoomScale);
        }
    }
}

public class AssetViewModel : ViewModelBase
{
    private readonly Asset _asset;
    public Asset Asset => _asset;
    public string Name => _asset.Name;
    public string Type => _asset.Type.ToString();
    public string Duration
    {
        get
        {
            if (_asset.Type == ClipType.Image) return "Image";
            var time = TimeSpan.FromSeconds(_asset.Duration);
            return $"{time.Minutes:D2}:{time.Seconds:D2}";
        }
    }
    public string Symbol => _asset.Type switch
    {
        ClipType.Video => "🎬",
        ClipType.Audio => "🎵",
        ClipType.Image => "🖼️",
        _ => "📝"
    };

    public AssetViewModel(Asset asset)
    {
        _asset = asset;
    }
}

public class ClipViewModel : ViewModelBase
{
    private readonly Clip _clip;
    private double _zoomScale;

    public Clip Clip => _clip;
    public string Id => _clip.Id;
    public string Name => Path.GetFileName(_clip.MediaRef);
    public bool IsAI => _clip.MediaType == ClipType.Text || Name.Contains("Generated") || Name.Contains("AI");
    public string Color => _clip.MediaType switch
    {
        ClipType.Audio => "green",
        _ => IsAI ? "accent" : "blue"
    };

    public double Left => _clip.StartFrame * _zoomScale;
    public double Width => _clip.DurationFrames * _zoomScale;

    public string ColorHex => Color switch
    {
        "green" => IsSelected ? "#1C452C" : "#11291A",
        "accent" => IsSelected ? "#3C1F5E" : "#24133A",
        _ => IsSelected ? "#1E3B5E" : "#12243C"
    };

    public string BorderHex => Color switch
    {
        "green" => IsSelected ? "#4ADE80" : "#2E8B57",
        "accent" => IsSelected ? "#81D4FA" : "#5A738E",
        _ => IsSelected ? "#2986F6" : "#1E5CA8"
    };

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(ColorHex));
                OnPropertyChanged(nameof(BorderHex));
            }
        }
    }

    public string Symbol => _clip.MediaType switch
    {
        ClipType.Video => "🎬",
        ClipType.Audio => "🎵",
        ClipType.Image => "🖼️",
        _ => "📝"
    };

    public ClipViewModel(Clip clip, double zoomScale)
    {
        _clip = clip;
        _zoomScale = zoomScale;
    }

    public void UpdateZoom(double zoomScale)
    {
        _zoomScale = zoomScale;
        OnPropertyChanged(nameof(Left));
        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(ColorHex));
        OnPropertyChanged(nameof(BorderHex));
    }
}

public class ChatMessageViewModel : ViewModelBase
{
    private string _text;
    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    public bool IsUser { get; }
    public string Time { get; }

    public string Alignment => IsUser ? "Right" : "Left";
    public string BackgroundHex => IsUser ? "#1E3B5E" : "#1C2430";
    public string ForegroundHex => IsUser ? "#F4F8FC" : "#D7E3F0";

    public ChatMessageViewModel(string text, bool isUser)
    {
        _text = text;
        IsUser = isUser;
        Time = DateTime.Now.ToString("HH:mm");
    }

    public void AppendDelta(string delta) => Text += delta;
}

public class TrackViewModel : ViewModelBase
{
    public string Id { get; }
    public string TypeLabel { get; }
    public ObservableCollection<ClipViewModel> Clips { get; } = new();

    public TrackViewModel(Track track)
    {
        Id = track.Id;
        TypeLabel = track.Type == ClipType.Video ? "VIDEO" : "AUDIO";
    }

    public void SyncClips(IReadOnlyList<Clip> coreClips, double zoomScale)
    {
        var currentIds = coreClips.Select(c => c.Id).ToHashSet();
        
        for (int i = Clips.Count - 1; i >= 0; i--)
        {
            if (!currentIds.Contains(Clips[i].Id))
                Clips.RemoveAt(i);
        }

        foreach (var clip in coreClips)
        {
            var existing = Clips.FirstOrDefault(c => c.Id == clip.Id);
            if (existing != null)
            {
                existing.UpdateZoom(zoomScale);
            }
            else
            {
                Clips.Add(new ClipViewModel(clip, zoomScale));
            }
        }
    }
}
