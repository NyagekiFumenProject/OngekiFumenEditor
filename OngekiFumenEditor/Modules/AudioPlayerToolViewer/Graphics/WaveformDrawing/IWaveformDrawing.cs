using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing
{
    public interface IWaveformDrawing : IDrawingTarget
    {
        IWaveformDrawingOption Options { get; }
        /// <summary>
        /// 绘制波形相关内容。<paramref name="drawWaveform"/> 为 false 时跳过波形折线本体（由调用方自行贴回预渲染图块），其余内容照常绘制。
        /// </summary>
        void Draw(IWaveformDrawingContext target, PeakPointCollection samplePeak, IDrawCommandListBuilder builder, bool drawWaveform = true);
    }
}
