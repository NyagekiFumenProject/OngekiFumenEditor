namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Acb;

internal sealed class AcbGenerateOption
{
    public int MusicId { get; set; } = -1;
    public string InputAudioFilePath { get; set; } = string.Empty;
    public string OutputFolderPath { get; set; } = string.Empty;
    public int PreviewBeginTime { get; set; } = 60000;
    public int PreviewEndTime { get; set; } = 80000;
}
