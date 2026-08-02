namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Jacket;

internal sealed class JacketGenerateOption
{
    public int MusicId { get; set; } = -1;
    public string OutputAssetbundleFolderPath { get; set; } = string.Empty;
    public string InputImageFilePath { get; set; } = string.Empty;
    public int Width { get; set; } = 520;
    public int Height { get; set; } = 520;
    public int WidthSmall { get; set; } = 220;
    public int HeightSmall { get; set; } = 220;
    public bool UpdateAssetBytesFile { get; set; } = true;
}
