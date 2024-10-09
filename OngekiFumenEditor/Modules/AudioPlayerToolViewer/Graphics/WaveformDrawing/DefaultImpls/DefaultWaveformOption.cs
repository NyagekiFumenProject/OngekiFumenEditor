﻿using OngekiFumenEditor.Base.Attributes;
using System.Text.Json.Serialization;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing.DefaultImpls
{
	public class DefaultWaveformOption : WaveformDrawingOptionBase
	{
		private bool showTimingLine = true;
		[ObjectPropertyBrowserShow]
		[JsonInclude]
		[ObjectPropertyBrowserAlias(nameof(ShowTimingLine))]
		public bool ShowTimingLine
		{
			get => showTimingLine;
			set => Set(ref showTimingLine, value);
		}

		private bool showObjectPlaceLine = true;
		[ObjectPropertyBrowserShow]
		[JsonInclude]
		[ObjectPropertyBrowserAlias(nameof(ShowObjectPlaceLine))]
		public bool ShowObjectPlaceLine
		{
			get => showObjectPlaceLine;
			set => Set(ref showObjectPlaceLine, value);
		}

        private bool showWaveform = true;
        [ObjectPropertyBrowserShow]
        [JsonInclude]
        [ObjectPropertyBrowserAlias(nameof(ShowWaveform))]
        public bool ShowWaveform
        {
            get => showWaveform;
            set => Set(ref showWaveform, value);
        }
    }
}
