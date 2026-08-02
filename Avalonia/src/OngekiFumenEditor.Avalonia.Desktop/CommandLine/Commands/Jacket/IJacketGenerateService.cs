namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Jacket;

internal interface IJacketGenerateService
{
    Task<JacketGenerateResult> GenerateAsync(
        JacketGenerateOption option,
        CancellationToken cancellationToken = default);

    Task<JacketImageData> GetMainImageDataAsync(
        byte[] abFileData,
        string filePath,
        CancellationToken cancellationToken = default);
}
