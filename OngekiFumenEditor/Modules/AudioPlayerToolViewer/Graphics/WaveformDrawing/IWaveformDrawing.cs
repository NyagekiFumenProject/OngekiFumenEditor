using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.Graphics;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing
{
	public interface IWaveformDrawing : IDrawing
	{
		IWaveformDrawingOption Options { get; }
		void Draw(IWaveformDrawingContext target, PeakPointCollection samplePeak);
	}
}
