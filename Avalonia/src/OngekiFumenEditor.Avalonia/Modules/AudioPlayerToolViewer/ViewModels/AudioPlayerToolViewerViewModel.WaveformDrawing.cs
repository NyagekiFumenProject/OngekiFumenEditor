using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.ViewModels
{
    public partial class AudioPlayerToolViewerViewModel
    {
        private IWaveformDrawing waveformDrawing;
        public IWaveformDrawing WaveformDrawing
        {
            get => waveformDrawing;
            set
            {
                SetProperty(ref waveformDrawing, value);
            }
        }

        private int resampleSize = Properties.AudioPlayerToolViewerSetting.Default.ResampleSize;
        public int ResampleSize
        {
            get => resampleSize;
            set
            {
                SetProperty(ref resampleSize, value);
                OnWaveformResampleSizeChanged();
                Properties.AudioPlayerToolViewerSetting.Default.ResampleSize = value;
                Properties.AudioPlayerToolViewerSetting.Default.Save();
            }
        }

        private float waveformVecticalScale = Properties.AudioPlayerToolViewerSetting.Default.WaveformVecticalScale;
        public float WaveformVecticalScale
        {
            get => waveformVecticalScale;
            set
            {
                SetProperty(ref waveformVecticalScale, value);
                Properties.AudioPlayerToolViewerSetting.Default.WaveformVecticalScale = value;
                Properties.AudioPlayerToolViewerSetting.Default.Save();
            }
        }

        private float durationMsPerPixel = Properties.AudioPlayerToolViewerSetting.Default.DurationMsPerPixel;
        public float DurationMsPerPixel
        {
            get => durationMsPerPixel;
            set
            {
                SetProperty(ref durationMsPerPixel, value);
                Properties.AudioPlayerToolViewerSetting.Default.DurationMsPerPixel = value;
                Properties.AudioPlayerToolViewerSetting.Default.Save();
            }
        }

        private float currentTimeXOffset = Properties.AudioPlayerToolViewerSetting.Default.CurrentTimeXOffset;
        public float CurrentTimeXOffset
        {
            get => currentTimeXOffset;
            set
            {
                SetProperty(ref currentTimeXOffset, value);
                Properties.AudioPlayerToolViewerSetting.Default.CurrentTimeXOffset = value;
                Properties.AudioPlayerToolViewerSetting.Default.Save();
            }
        }

        private bool isShowWaveform = true;
        public bool IsShowWaveform
        {
            get => isShowWaveform && Properties.AudioPlayerToolViewerSetting.Default.EnableWaveformDisplay;
            set => SetProperty(ref isShowWaveform, value);
        }

        private int limitFPS = Properties.AudioPlayerToolViewerSetting.Default.LimitFPS;
        public int LimitFPS
        {
            get => limitFPS;
            set
            {
                SetProperty(ref limitFPS, value);
                Properties.AudioPlayerToolViewerSetting.Default.LimitFPS = value;
                Properties.AudioPlayerToolViewerSetting.Default.Save();
            }
        }

        public FumenVisualEditorViewModel EditorViewModel => Editor;

        public void OnWaveformOptionReset()
        {
            WaveformDrawing?.Options?.Reset();
        }

        public void OnWaveformOptionSave()
        {
            WaveformDrawing?.Options?.Save();
        }
    }
}
