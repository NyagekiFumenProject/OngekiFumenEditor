using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;

namespace OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing;

public interface IWaveformDrawing : IDrawingTarget
{
    IWaveformDrawingOption Options { get; }
    void Draw(IWaveformDrawingContext target, PeakPointCollection samplePeak);
}
