using OngekiFumenEditor.Base.Attributes;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing.DefaultImpls
{
    public class DefaultWaveformOption : WaveformDrawingOptionBase
    {
        private bool showTimingLine = true;
        [ObjectPropertyBrowserShow]
        [JsonInclude]
        public bool ShowTimingLine
        {
            get => showTimingLine;
            set => Set(ref showTimingLine, value);
        }

        private bool showObjectPlaceLine = true;
        [ObjectPropertyBrowserShow]
        [JsonInclude]
        public bool ShowObjectPlaceLine
        {
            get => showObjectPlaceLine;
            set => Set(ref showObjectPlaceLine, value);
        }
    }
}
