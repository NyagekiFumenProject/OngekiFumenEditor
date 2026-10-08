using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Properties;
using System.Text.Json.Serialization;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing.DefaultImpls
{
    public class DefaultWaveformOption : WaveformDrawingOptionBase
    {
        private bool showTimingLine;
        [ObjectPropertyBrowserShow]
        [LocalizableObjectPropertyBrowserAlias(nameof(ShowTimingLine))]
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
        [LocalizableObjectPropertyBrowserAlias(nameof(ShowObjectPlaceLine))]
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
        [LocalizableObjectPropertyBrowserAlias(nameof(ShowWaveform))]
        public bool ShowWaveform
        {
            get => showWaveform;
            set
            {
                Set(ref showWaveform, value);
                DefaultWaveformSettings.Default.ShowWaveform = value;
            }
        }

        private bool showRhythmCurve;
        [ObjectPropertyBrowserShow]
        [LocalizableObjectPropertyBrowserAlias(nameof(ShowRhythmCurve))]
        public bool ShowRhythmCurve
        {
            get => showRhythmCurve;
            set
            {
                Set(ref showRhythmCurve, value);
                DefaultWaveformSettings.Default.ShowRhythmCurve = value;
            }
        }




        private float rhythmCurveGamma = DefaultWaveformSettings.Default.RhythmCurveGamma;
        [ObjectPropertyBrowserShow]
        [LocalizableObjectPropertyBrowserAlias(nameof(RhythmCurveGamma))]
        public float RhythmCurveGamma
        {
            get => rhythmCurveGamma;
            set
            {
                var clamped = System.Math.Clamp(value, RhythmCurveTone.MinGamma, RhythmCurveTone.MaxGamma);
                if (Set(ref rhythmCurveGamma, clamped))
                    DefaultWaveformSettings.Default.RhythmCurveGamma = clamped;
            }
        }

        private float rhythmCurveEmphasis = DefaultWaveformSettings.Default.RhythmCurveEmphasis;
        [ObjectPropertyBrowserShow]
        [LocalizableObjectPropertyBrowserAlias(nameof(RhythmCurveEmphasis))]
        public float RhythmCurveEmphasis
        {
            get => rhythmCurveEmphasis;
            set
            {
                var clamped = System.Math.Clamp(value, 0f, RhythmCurveTone.MaxEmphasis);
                if (Set(ref rhythmCurveEmphasis, clamped))
                    DefaultWaveformSettings.Default.RhythmCurveEmphasis = clamped;
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
            ShowRhythmCurve = DefaultWaveformSettings.Default.ShowRhythmCurve;
            RhythmCurveGamma = DefaultWaveformSettings.Default.RhythmCurveGamma;
            RhythmCurveEmphasis = DefaultWaveformSettings.Default.RhythmCurveEmphasis;
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
