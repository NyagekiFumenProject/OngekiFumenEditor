namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Updater;

internal interface IProgramUpdateService
{
    Task<ProgramUpdateResult> UpdateAsync(
        UpdaterOption option,
        CancellationToken cancellationToken = default);
}
