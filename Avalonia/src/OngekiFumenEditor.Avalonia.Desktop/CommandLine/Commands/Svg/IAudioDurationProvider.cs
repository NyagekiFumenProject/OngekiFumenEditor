namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Svg;

internal interface IAudioDurationProvider
{
    Task<TimeSpan> GetDurationAsync(string audioFilePath, CancellationToken cancellationToken);
}
