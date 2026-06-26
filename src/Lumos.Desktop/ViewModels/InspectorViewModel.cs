using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Lumos.Domain;

namespace Lumos.Desktop.ViewModels;

public class InspectorViewModel : INotifyPropertyChanged
{
    private string? _clipId;
    public string? ClipId
    {
        get => _clipId;
        set { _clipId = value; OnPropertyChanged(); }
    }

    private string _clipName = "No Selection";
    public string ClipName
    {
        get => _clipName;
        set { _clipName = value; OnPropertyChanged(); }
    }

    public ObservableCollection<EffectViewModel> Effects { get; } = new();

    public void Refresh(Timeline timeline, string? selectedClipId)
    {
        ClipId = selectedClipId;
        Effects.Clear();

        if (string.IsNullOrEmpty(selectedClipId))
        {
            ClipName = "No Selection";
            return;
        }

        var clip = timeline.Tracks.SelectMany(t => t.Clips).FirstOrDefault(c => c.Id == selectedClipId);
        if (clip == null)
        {
            ClipName = "Clip not found";
            return;
        }

        ClipName = $"Clip: {clip.MediaRef}";
        foreach (var effect in clip.Effects)
        {
            Effects.Add(new EffectViewModel(effect));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public class EffectViewModel : INotifyPropertyChanged
{
    private readonly Effect _effect;

    public string Id => _effect.Id;
    public string Name => _effect.Type;
    public bool IsEnabled
    {
        get => _effect.Enabled;
        set { _effect.Enabled = value; OnPropertyChanged(); }
    }

    public EffectViewModel(Effect effect)
    {
        _effect = effect;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
