using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Graphics;

public interface IWaveformDrawingContext : IDrawingContext
{
    TimeSpan CurrentTime { get; }
    TimeSpan AudioTotalDuration { get; }
    float DurationMsPerPixel { get; }
    float CurrentTimeXOffset { get; }
    float WaveformVecticalScale { get; }

    FumenVisualEditorViewModel EditorViewModel { get; }
}
