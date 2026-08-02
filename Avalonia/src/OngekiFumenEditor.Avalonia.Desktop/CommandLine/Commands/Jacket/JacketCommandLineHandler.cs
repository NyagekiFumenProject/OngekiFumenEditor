using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Jacket;

[RegisterSingleton<ICommandLineHandler<JacketGenerateOption>>]
internal sealed class JacketCommandLineHandler : ICommandLineHandler<JacketGenerateOption>
{
    internal const int RelativePathExitCode = -5;
    internal const int GenerationFailedExitCode = -6;

    private readonly IJacketGenerateService jacketGenerateService;
    private readonly ICommandLineOutput output;

    public JacketCommandLineHandler(
        IJacketGenerateService jacketGenerateService,
        ICommandLineOutput output)
    {
        this.jacketGenerateService = jacketGenerateService;
        this.output = output;
    }

    public async Task<int> HandleAsync(JacketGenerateOption options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Path.IsPathFullyQualified(options.InputImageFilePath) ||
            !Path.IsPathFullyQualified(options.OutputAssetbundleFolderPath))
        {
            await output.WriteErrorLineAsync(Lang.CliArgumentNotAbsolutePath);
            return RelativePathExitCode;
        }

        JacketGenerateResult result;
        try
        {
            result = await jacketGenerateService.GenerateAsync(options, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            result = new JacketGenerateResult(false, exception.Message);
        }

        if (result.IsSuccess)
            return 0;

        await output.WriteErrorLineAsync($"{Lang.GenerateJacketFileFail} {result.Message}");
        return GenerationFailedExitCode;
    }
}
