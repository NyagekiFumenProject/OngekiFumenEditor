using OngekiFumenEditor.Base.Attributes;
using System.Text.Json.Serialization;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing.DefaultImpls
{
	public class DefaultWaveformOption : WaveformDrawingOptionBase
	{
		private bool showTimingLine = true;
		[ObjectPropertyBrowserShow]
		[JsonInclude]
		[ObjectPropertyBrowserAlias("显示节奏线")]
		public bool ShowTimingLine
		{
			get => showTimingLine;
			set => Set(ref showTimingLine, value);
		}

		private bool showObjectPlaceLine = true;
		[ObjectPropertyBrowserShow]
		[JsonInclude]
		[ObjectPropertyBrowserAlias("显示物品放置的位置")]
		public bool ShowObjectPlaceLine
		{
			get => showObjectPlaceLine;
			set => Set(ref showObjectPlaceLine, value);
		}
	}
}
