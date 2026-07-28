using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing.DefaultImpls;

public class DefaultWaveformOption : WaveformDrawingOptionBase
{
    private bool showTimingLine;
    public bool ShowTimingLine
    {
        get => showTimingLine;
        set
        {
            SetProperty(ref showTimingLine, value);
            DefaultWaveformSettings.Default.ShowTimingLine = value;
        }
    }

    private bool showObjectPlaceLine;
    public bool ShowObjectPlaceLine
    {
        get => showObjectPlaceLine;
        set
        {
            SetProperty(ref showObjectPlaceLine, value);
            DefaultWaveformSettings.Default.ShowObjectPlaceLine = value;
        }
    }

    private bool showWaveform;
    public bool ShowWaveform
    {
        get => showWaveform;
        set
        {
            SetProperty(ref showWaveform, value);
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

