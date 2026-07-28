using Gekimini.Avalonia.Framework;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer;

public interface IAudioPlayerToolViewer : IToolViewModel, IDisposable
{
    IAudioPlayer AudioPlayer { get; }
    float SoundVolume { get; set; }
    FumenVisualEditorViewModel Editor { get; }
    void RequestPlayOrPause();
}
