using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Lumos.Domain;
using Lumos.Application;
using Lumos.Application.Assets;
using Lumos.Application.State;
using Lumos.Application.Commands;

namespace Lumos.Desktop.ViewModels;

public record EffectItemViewModel(string DisplayName, string EffectId, string Icon);

public class MainWindowViewModel : ViewModelBase
{
    private string _projectName = "Untitled Project";
    
    // ── Folder tree state ──────────────────────────────────────────
    
    public ObservableCollection<FolderViewModel> FolderRoots { get; } = new();
    
    private string? _selectedFolderId;
    public string? SelectedFolderId
    {
        get => _selectedFolderId;
        set
        {
            if (SetProperty(ref _selectedFolderId, value))
            {
                RefreshAssets();
                OnPropertyChanged(nameof(IsAllAssetsSelected));
                OnPropertyChanged(nameof(SelectedFolderName));
            }
        }
    }
    
    public bool IsAllAssetsSelected => SelectedFolderId == null;
    public string SelectedFolderName => SelectedFolderId switch
    {
        null => "All Assets",
        string id => App.MediaFolderStore.TryGet(id, out var f) ? f!.Name : "All Assets"
    };
    
    public void SelectFolder(string? folderId)
    {
        SelectedFolderId = folderId;
        // Update visual selection state in tree
        foreach (var root in FolderRoots)
            UpdateFolderSelection(root, folderId);
    }
    
    private static bool UpdateFolderSelection(FolderViewModel folder, string? selectedId)
    {
        bool isSelected = folder.Id == selectedId;
        folder.IsSelected = isSelected;
        foreach (var child in folder.Children)
            isSelected |= UpdateFolderSelection(child, selectedId);
        return isSelected;
    }
    
    public void RefreshFolderTree()
    {
        var allFolders = App.MediaFolderStore.GetAll().ToList();
        var rootFolders = allFolders.Where(f => f.ParentId == null).OrderBy(f => f.Name).ToList();
        
        FolderRoots.Clear();
        foreach (var root in rootFolders)
        {
            var vm = BuildFolderTree(root, allFolders);
            FolderRoots.Add(vm);
        }
        
        // Restore selection if still valid
        if (_selectedFolderId != null && allFolders.All(f => f.Id != _selectedFolderId))
            _selectedFolderId = null;
    }
    
    private FolderViewModel BuildFolderTree(MediaFolderData folder, List<MediaFolderData> allFolders)
    {
        var vm = new FolderViewModel(folder, fid =>
            App.AssetManager.Catalog.GetAll().Count(a => a.FolderId == fid));
        
        var children = allFolders.Where(f => f.ParentId == folder.Id).OrderBy(f => f.Name);
        foreach (var child in children)
            vm.Children.Add(BuildFolderTree(child, allFolders));
        
        return vm;
    }
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
                OnPropertyChanged(nameof(TimeCodeShort));
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
            int fps = Fps > 0 ? Fps : 30;
            int hours = PlayheadFrame / (fps * 3600);
            int minutes = (PlayheadFrame / (fps * 60)) % 60;
            int seconds = (PlayheadFrame / fps) % 60;
            int frames = PlayheadFrame % fps;
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}:{frames:D2}";
        }
    }

    /// Shorter timecode for compact display.
    public string TimeCodeShort => TimeCode.Length > 8 ? TimeCode[3..] : TimeCode;

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

    // ── Snap indicator state ────────────────────────────────────────────

    private double? _snapLineX;
    public double? SnapLineX
    {
        get => _snapLineX;
        set => SetProperty(ref _snapLineX, value);
    }

    private int? _snappedFrame;
    public int? SnappedFrame
    {
        get => _snappedFrame;
        set => SetProperty(ref _snappedFrame, value);
    }

    private string _snapLabel = "";
    public string SnapLabel
    {
        get => _snapLabel;
        set => SetProperty(ref _snapLabel, value);
    }

    // ── Context menu state ─────────────────────────────────────────────

    private bool _isContextMenuOpen;
    public bool IsContextMenuOpen
    {
        get => _isContextMenuOpen;
        set => SetProperty(ref _isContextMenuOpen, value);
    }

    private string? _contextMenuClipId;
    public string? ContextMenuClipId
    {
        get => _contextMenuClipId;
        set => SetProperty(ref _contextMenuClipId, value);
    }

    private string? _contextMenuClipName;
    public string? ContextMenuClipName
    {
        get => _contextMenuClipName;
        set => SetProperty(ref _contextMenuClipName, value);
    }

    private string? _contextMenuTrackId;
    public string? ContextMenuTrackId
    {
        get => _contextMenuTrackId;
        set => SetProperty(ref _contextMenuTrackId, value);
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
        AIChatMessages.Add(new ChatMessageViewModel("Hi! I'm Lumos AI, your creative co-editor. Tell me what you'd like to do, or select a quick action below.", false));
        McpActivityLogs.Add("[System] MCP server initialized and ready.");

        App.EditorStore.StateChanged += OnStateChanged;
        App.AssetManager.AssetAdded += OnAssetAdded;
        App.AssetManager.AssetRemoved += OnAssetRemoved;
        App.CommandQueue.CommandCompleted += OnCommandCompleted;

        LoadState(App.EditorStore.State);
    }

    private void OnStateChanged(object? sender, StateChangedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            LoadState(App.EditorStore.State);
            ActiveToolMode = App.EditorStore.State.ToolMode;
        });
    }

    private void OnAssetAdded(object? sender, AssetEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            App.VideoEngine.Rebuild();
            RefreshAssets();
            RefreshFolderTree();
        });
    }

    private void OnAssetRemoved(object? sender, AssetEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            App.VideoEngine.Rebuild();
            RefreshAssets();
            RefreshFolderTree();
        });
    }

    private void OnCommandCompleted(object? sender, CommandCompletedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            string logMsg = $"[{DateTime.Now:HH:mm:ss}] CommandExecuted: \"{e.Command.Label}\" -> Succeeded: {e.Result.Succeeded}";
            McpActivityLogs.Insert(0, logMsg);
            App.VideoEngine.Rebuild();
        });
    }

    public void RefreshAssets()
    {
        Assets.Clear();
        var allAssets = App.AssetManager.Catalog.GetAll();
        foreach (var asset in allAssets)
        {
            if (_selectedFolderId == null || asset.FolderId == _selectedFolderId)
                Assets.Add(new AssetViewModel(asset));
        }
        HasAssets = Assets.Count > 0;
    }

    // ── Preview quality ─────────────────────────────────────────────

    private PreviewQuality _previewQuality = PreviewQuality.Half;
    public PreviewQuality PreviewQuality
    {
        get => _previewQuality;
        set
        {
            if (SetProperty(ref _previewQuality, value))
            {
                App.EditorStore.UpdatePreviewQuality(value);
                App.VideoEngine.Rebuild();
                OnPropertyChanged(nameof(IsFullQuality));
                OnPropertyChanged(nameof(IsHalfQuality));
                OnPropertyChanged(nameof(IsQuarterQuality));
                OnPropertyChanged(nameof(PreviewQualityLabel));
            }
        }
    }

    public bool IsFullQuality => PreviewQuality == PreviewQuality.Full;
    public bool IsHalfQuality => PreviewQuality == PreviewQuality.Half;
    public bool IsQuarterQuality => PreviewQuality == PreviewQuality.Quarter;
    public string PreviewQualityLabel => PreviewQuality switch
    {
        PreviewQuality.Full => "Full",
        PreviewQuality.Half => "1/2",
        PreviewQuality.Quarter => "1/4",
        _ => "1/2"
    };

    public void ShowAllAssets()
    {
        SelectFolder(null);
        RefreshAssets();
    }

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

    // ── Timeline commands ──────────────────────────────────────────────

    public void SelectClip(string? clipId, string? trackId, bool additive = false)
    {
        App.EditorStore.UpdateSelection(s =>
        {
            if (clipId == null) return s.ClearAll();
            return additive ? s.SelectClip(clipId, true) : s.SelectClip(clipId, false);
        });

        var timeline = App.EditorStore.State.Timeline.Timeline;
        Inspector.Refresh(timeline, clipId);

        // Update ClipViewModel selection state
        foreach (var track in Tracks)
            foreach (var clip in track.Clips)
                clip.IsSelected = App.EditorStore.State.Selection.IsClipSelected(clip.Id);
    }

    public void SelectTrack(string? trackId)
    {
        App.EditorStore.UpdateSelection(s =>
            trackId == null ? s.ClearAll() : s.SetActiveTrack(trackId));
    }

    public void SeekToFrame(int frame)
    {
        var total = App.EditorStore.State.TotalFrames;
        App.VideoEngine.Seek(frame);
    }

    public void TogglePlayPause()
    {
        App.VideoEngine.TogglePlayback();
    }

    public void ToggleTool(ToolMode mode)
    {
        App.EditorStore.SetToolMode(
            App.EditorStore.State.ToolMode == mode ? ToolMode.Pointer : mode);
    }

    public void ShowContextMenu(string clipId, string clipName, string trackId)
    {
        ContextMenuClipId = clipId;
        ContextMenuClipName = clipName;
        ContextMenuTrackId = trackId;
        IsContextMenuOpen = true;
    }

    public void HandleContextAction(string actionId)
    {
        string clipId = ContextMenuClipId ?? "";
        IsContextMenuOpen = false;
        if (string.IsNullOrEmpty(clipId)) return;

        McpActivityLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] ContextAction: {actionId} on clip {clipId}");

        switch (actionId)
        {
            case "delete":
                App.CommandQueue.Enqueue(new RemoveClipsAsyncCommand(new[] { clipId }));
                break;
            case "split":
                App.CommandQueue.Enqueue(new SplitClipAsyncCommand(clipId, PlayheadFrame));
                break;
            case "inspector":
                ActiveAITab = "Inspector";
                var timeline = App.EditorStore.State.Timeline.Timeline;
                Inspector.Refresh(timeline, clipId);
                break;
        }
    }

    public void HandleTrackMute(string trackId, bool isMuted)
    {
        App.EditorStore.MuteTimeline("Toggle Mute", trackId);
    }

    public void HandleTrackHide(string trackId, bool isHidden)
    {
        App.EditorStore.HideTimeline("Toggle Hide", trackId);
    }

    public void HandleTrackLock(string trackId, bool isLocked)
    {
        App.EditorStore.LockTimeline("Toggle Lock", trackId);
    }

    public void ZoomToFit()
    {
        if (TotalFrames <= 0) return;
        double targetPpf = 1200.0 / TotalFrames;
        ZoomScale = Math.Clamp(targetPpf, Domain.Zoom.Min, Domain.Zoom.Max);
    }

    public void ZoomIn()
    {
        ZoomScale = Math.Min(ZoomScale * 1.25, Domain.Zoom.Max);
    }

    public void ZoomOut()
    {
        ZoomScale = Math.Max(ZoomScale / 1.25, Domain.Zoom.Min);
    }

    // ── Project persistence ────────────────────────────────────────

    private string? _projectDir;
    public string? ProjectDir
    {
        get => _projectDir;
        set => SetProperty(ref _projectDir, value);
    }

    public void SaveProject(string? dir = null)
    {
        dir ??= ProjectDir;
        if (dir == null)
        {
            dir = App.RecentProjects.Entries.Count > 0
                ? App.RecentProjects.Entries[0].Directory
                : null;
        }
        if (dir == null)
        {
            McpActivityLogs.Insert(0, "[Project] No save location. Use Save As.");
            return;
        }

        var data = ProjectDataBuilder.BuildFromState(App.EditorStore.State, App.MediaFolderStore, App.AssetManager);
        App.ProjectSerializer.Save(dir, data);
        App.EditorStore.MarkClean();
        ProjectDir = dir;
        App.RecentProjects.RecordOpen(dir, ProjectName);
        McpActivityLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Project saved to {dir}");
    }

    public void SaveProjectAs(string dir)
    {
        SaveProject(dir);
    }

    public void OpenProject(string dir)
    {
        var data = App.ProjectSerializer.Load(dir);
        if (data == null)
        {
            McpActivityLogs.Insert(0, $"[Project] Failed to load project from {dir}");
            return;
        }

        var project = data.Project;
        var timeline = project.Timelines.Count > 0 ? project.Timelines[0] : new Timeline
        {
            Width = project.Width, Height = project.Height, Fps = project.FrameRate
        };

        App.EditorStore.SetProject(project.Id, project.Name, timeline);
        App.VideoEngine.Rebuild();

        // Restore folder hierarchy from the project package
        ProjectDataBuilder.RestoreFolders(App.MediaFolderStore, data.Folders);
        RefreshFolderTree();

        ProjectDir = dir;
        App.RecentProjects.RecordOpen(dir, project.Name);
        App.Autosave.Start(dir);

        McpActivityLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Project loaded: {project.Name}");
    }

    public void NewProject()
    {
        App.Autosave.Stop();

        var projectId = Guid.NewGuid();
        var timeline = new Timeline { Width = 1920, Height = 1080, Fps = 30 };
        App.EditorStore.SetProject(projectId, "Untitled Project", timeline);
        App.VideoEngine.Rebuild();

        // Reset folder store to defaults for the new project
        App.MediaFolderStore.Clear();
        RefreshFolderTree();

        ProjectDir = null;
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "LumosProjects", "current");
        Directory.CreateDirectory(dir);
        App.Autosave.Start(dir);

        McpActivityLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] New project created");
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

        for (int i = Tracks.Count - 1; i >= 0; i--)
        {
            if (!currentIds.Contains(Tracks[i].Id))
                Tracks.RemoveAt(i);
        }

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
                int currentIndex = Tracks.IndexOf(existingTrack);
                if (currentIndex != i)
                {
                    Tracks.Move(currentIndex, i);
                }
            }

            existingTrack.SyncClips(coreTrack.Clips, ZoomScale);
            existingTrack.IsMuted = coreTrack.IsMuted;
            existingTrack.IsHidden = coreTrack.IsHidden;
            existingTrack.IsLocked = coreTrack.IsSyncLocked;
        }
    }
}

public class FolderViewModel : ViewModelBase
{
    private readonly MediaFolderData _data;
    private readonly Func<string, int> _getAssetCount;

    public string Id => _data.Id;
    public string Name => _data.Name;
    public string? ParentId => _data.ParentId;

    public ObservableCollection<FolderViewModel> Children { get; } = new();

    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                OnPropertyChanged(nameof(ExpandIcon));
            }
        }
    }

    public string ExpandIcon => IsExpanded ? "▾" : "▸";

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(BackgroundHex));
            }
        }
    }

    public string BackgroundHex => IsSelected ? "#1E3A5F" : "Transparent";

    public int AssetCount => _getAssetCount(Id);

    public string Icon => _data.Name switch
    {
        "Imports" => "📁",
        "Generated" => "⚡",
        "Music" => "🎵",
        _ => "📂"
    };

    public bool HasChildren => Children.Count > 0;

    public FolderViewModel(MediaFolderData data, Func<string, int> getAssetCount)
    {
        _data = data;
        _getAssetCount = getAssetCount;
    }

    public void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
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
    public string TypeColor => _asset.Type switch
    {
        ClipType.Video => "#3B82F6",
        ClipType.Audio => "#10B981",
        ClipType.Image => "#F59E0B",
        _ => "#A855F7"
    };
    public string Resolution => _asset.SourceWidth is > 0 && _asset.SourceHeight is > 0
        ? $"{_asset.SourceWidth}×{_asset.SourceHeight}"
        : "";
    public string FpsText => _asset.SourceFps is > 0
        ? $"{_asset.SourceFps:F1}fps"
        : "";

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
    public ClipType MediaType => _clip.MediaType;
    public bool IsAI => _clip.MediaType == ClipType.Text || Name.Contains("Generated") || Name.Contains("AI");
    public bool IsVideo => _clip.MediaType == ClipType.Video;
    public bool IsAudio => _clip.MediaType == ClipType.Audio;
    public bool IsImage => _clip.MediaType == ClipType.Image;
    public bool IsText => _clip.MediaType == ClipType.Text;

    public string Symbol => _clip.MediaType switch
    {
        ClipType.Video => "🎬",
        ClipType.Audio => "🎵",
        ClipType.Image => "🖼️",
        _ => "📝"
    };

    public string TrackType => _clip.MediaType == ClipType.Audio ? "AUDIO" : "VIDEO";

    // ── Computed layout properties ────────────────────────────────────

    public double Left => _clip.StartFrame * _zoomScale;
    public double Width => Math.Max(2, _clip.DurationFrames * _zoomScale);

    public string DurationText
    {
        get
        {
            double seconds = (double)_clip.DurationFrames / 30;
            int min = (int)(seconds / 60);
            double sec = seconds % 60;
            return min > 0 ? $"{min}:{sec:F1}" : $"{sec:F1}s";
        }
    }

    public string TimeRangeText
    {
        get
        {
            string start = FormatFrame(_clip.StartFrame);
            string end = FormatFrame(_clip.EndFrame);
            return $"{start} → {end}";
        }
    }

    public string StartFrameText => $"F:{_clip.StartFrame}";
    public string DurationFramesText => $"{_clip.DurationFrames}f";
    public string SpeedText => _clip.Speed != 1.0 ? $"{_clip.Speed:F2}×" : "";
    public string OpacityText => _clip.Opacity < 1.0 ? $"{(int)(_clip.Opacity * 100)}%" : "";
    public string FadeInText => _clip.FadeInFrames > 0 ? $"In:{_clip.FadeInFrames}f" : "";
    public string FadeOutText => _clip.FadeOutFrames > 0 ? $"Out:{_clip.FadeOutFrames}f" : "";
    public int EffectCount => _clip.Effects.Count;
    public bool HasEffects => _clip.Effects.Count > 0;
    public string HasAudioBadge => _clip.SourceClipType == ClipType.Video ? "🔊" : "";

    // ── Transform properties for inspector ────────────────────────────

    public double PositionX => _clip.Transform.CenterX;
    public double PositionY => _clip.Transform.CenterY;
    public double ScaleX => _clip.Transform.Width;
    public double ScaleY => _clip.Transform.Height;
    public double Rotation => _clip.Transform.Rotation;
    public double CropLeft => _clip.Crop.Left;
    public double CropRight => _clip.Crop.Right;
    public double CropTop => _clip.Crop.Top;
    public double CropBottom => _clip.Crop.Bottom;
    public double FadeInFrames => _clip.FadeInFrames;
    public double FadeOutFrames => _clip.FadeOutFrames;
    public double Speed => _clip.Speed;

    // ── Selection state ───────────────────────────────────────────────

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
                OnPropertyChanged(nameof(BorderWidth));
            }
        }
    }

    public string ColorHex => (_clip.MediaType, IsSelected) switch
    {
        (ClipType.Audio, true)  => "#1C452C",
        (ClipType.Audio, false) => "#11291A",
        (ClipType.Text, true)   => "#3C1F5E",
        (ClipType.Text, false)  => "#24133A",
        (ClipType.Image, true)  => "#3D3020",
        (ClipType.Image, false) => "#2A2015",
        (_, true)               => "#1E3B5E",
        _                       => "#12243C",
    };

    public string BorderHex => (_clip.MediaType, IsSelected) switch
    {
        (ClipType.Audio, true)  => "#4ADE80",
        (ClipType.Audio, false) => "#2E8B57",
        (_, true)               => "#2986F6",
        _                       => "#1E5CA8",
    };

    public double BorderWidth => IsSelected ? 2.0 : 1.0;

    public string TypeColorHex => _clip.MediaType switch
    {
        ClipType.Audio => "#10B981",
        ClipType.Text => "#A855F7",
        ClipType.Image => "#F59E0B",
        _ => "#3B82F6"
    };

    // ── Constructor & updates ─────────────────────────────────────────

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
        OnPropertyChanged(nameof(BorderWidth));
    }

    private static string FormatFrame(int frame)
    {
        int fps = 30;
        int hours = frame / (fps * 3600);
        int minutes = (frame / (fps * 60)) % 60;
        int seconds = (frame / fps) % 60;
        int frames = frame % fps;
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}:{frames:D2}";
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
    private string _name;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
    public string TypeLabel { get; }
    public ObservableCollection<ClipViewModel> Clips { get; } = new();

    private bool _isMuted;
    public bool IsMuted
    {
        get => _isMuted;
        set => SetProperty(ref _isMuted, value);
    }

    private bool _isHidden;
    public bool IsHidden
    {
        get => _isHidden;
        set => SetProperty(ref _isHidden, value);
    }

    private bool _isLocked;
    public bool IsLocked
    {
        get => _isLocked;
        set => SetProperty(ref _isLocked, value);
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string TrackId => Id;
    public Track DomainTrack => _track;
    private readonly Track _track;

    public TrackViewModel(Track track)
    {
        _track = track;
        Id = track.Id;
        _name = track.Name;
        TypeLabel = track.Type switch
        {
            ClipType.Video => "VIDEO",
            ClipType.Audio => "AUDIO",
            ClipType.Text => "TEXT",
            ClipType.Image => "IMAGE",
            _ => "VIDEO"
        };
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
