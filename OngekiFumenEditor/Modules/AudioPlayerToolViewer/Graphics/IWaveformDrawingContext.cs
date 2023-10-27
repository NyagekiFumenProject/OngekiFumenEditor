using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics
{
	public interface IWaveformDrawingContext : IDrawingContext
	{
		/// <summary>
		/// 表示当前时间
		/// </summary>
		TimeSpan CurrentTime { get; }
		TimeSpan AudioTotalDuration { get; }
		/// <summary>
		/// 表示每个像素显示时间间距
		/// </summary>
		float DurationMsPerPixel { get; }
		float CurrentTimeXOffset { get; }
		float WaveformVecticalScale { get; }

		FumenVisualEditorViewModel EditorViewModel { get; }
	}
}
