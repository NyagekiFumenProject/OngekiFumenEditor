namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Acb;

internal interface IAcbGenerateService
{
    Task<AcbGenerateResult> GenerateAsync(
        AcbGenerateOption option,
        CancellationToken cancellationToken = default);
}
