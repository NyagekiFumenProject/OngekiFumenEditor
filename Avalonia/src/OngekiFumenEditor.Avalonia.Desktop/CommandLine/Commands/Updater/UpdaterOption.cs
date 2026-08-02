namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Updater;

internal sealed class UpdaterOption
{
    public string SourceFolder { get; set; } = string.Empty;
    public string TargetFolder { get; set; } = string.Empty;
    public string SourceVersion { get; set; } = string.Empty;
}
