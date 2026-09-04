using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;

namespace OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing;

public abstract class CommonWaveformDrawingBase : CommonDrawingBase, IWaveformDrawing
{
    public abstract IWaveformDrawingOption Options { get; }
    public abstract void Draw(IWaveformDrawingContext target, PeakPointCollection samplePeak, IDrawCommandListBuilder builder);
    public abstract void Initialize(IRenderManagerImpl impl);
}
