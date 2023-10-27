using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing
{
	public abstract class CommonWaveformDrawingBase : CommonDrawingBase, IWaveformDrawing
	{
		public abstract IWaveformDrawingOption Options { get; }
		public abstract void Draw(IWaveformDrawingContext target, PeakPointCollection samplePeak);
	}
}
