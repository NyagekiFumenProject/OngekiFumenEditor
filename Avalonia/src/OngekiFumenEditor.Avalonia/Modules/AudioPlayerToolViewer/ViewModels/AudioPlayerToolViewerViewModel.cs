using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.Tools;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.ViewModels;

[RegisterSingleton<IAudioPlayerToolViewer>]
public class AudioPlayerToolViewerViewModel : ToolViewModelBase, IAudioPlayerToolViewer
{
    public AudioPlayerToolViewerViewModel() : base("Audio Player".ToLocalizedStringByRawText())
    {
        Dock = global::Dock.Model.Core.DockMode.Bottom;
    }

    public IAudioPlayer AudioPlayer => null;
    public float SoundVolume { get; set; }
    public FumenVisualEditorViewModel Editor => null;

    public void RequestPlayOrPause()
    {
    }

    public void Dispose()
    {
    }
}