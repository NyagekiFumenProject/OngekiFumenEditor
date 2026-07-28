using CommunityToolkit.Mvvm.ComponentModel;

namespace OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing.DefaultImpls;

public abstract class WaveformDrawingOptionBase : ObservableObject, IWaveformDrawingOption
{
    public abstract void Reload();
    public abstract void Reset();
    public abstract void Save();
}
