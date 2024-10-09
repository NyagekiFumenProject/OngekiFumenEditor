using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Properties;
using System.Text.Json.Serialization;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing.DefaultImpls
{
    public class DefaultWaveformOption : WaveformDrawingOptionBase
    {
        private bool showTimingLine;
        [ObjectPropertyBrowserShow]
        [ObjectPropertyBrowserAlias(nameof(ShowTimingLine))]
        public bool ShowTimingLine
        {
            get => showTimingLine;
            set
            {
                Set(ref showTimingLine, value);
                DefaultWaveformSettings.Default.ShowTimingLine = value;
            }
        }

        private bool showObjectPlaceLine;
        [ObjectPropertyBrowserShow]
        [ObjectPropertyBrowserAlias(nameof(ShowObjectPlaceLine))]
        public bool ShowObjectPlaceLine
        {
            get => showObjectPlaceLine;
            set
            {
                Set(ref showObjectPlaceLine, value);
                DefaultWaveformSettings.Default.ShowObjectPlaceLine = value;
            }
        }

        private bool showWaveform;
        [ObjectPropertyBrowserShow]
        [ObjectPropertyBrowserAlias(nameof(ShowWaveform))]
        public bool ShowWaveform
        {
            get => showWaveform;
            set
            {
                Set(ref showWaveform, value);
                DefaultWaveformSettings.Default.ShowWaveform = value;
            }
        }

        public DefaultWaveformOption()
        {
            SyncFromSettings();
        }

        private void SyncFromSettings()
        {
            ShowWaveform = DefaultWaveformSettings.Default.ShowWaveform;
            ShowObjectPlaceLine = DefaultWaveformSettings.Default.ShowObjectPlaceLine;
            ShowTimingLine = DefaultWaveformSettings.Default.ShowTimingLine;
        }

        public override void Reload()
        {
            DefaultWaveformSettings.Default.Reload();
            SyncFromSettings();
        }

        public override void Reset()
        {
            DefaultWaveformSettings.Default.Reset();
            SyncFromSettings();
        }

        public override void Save()
        {
            DefaultWaveformSettings.Default.Save();
        }
    }
}
