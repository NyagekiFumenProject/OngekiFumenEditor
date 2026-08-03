using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Acb;

[RegisterSingleton<ICommandLineHandler<AcbGenerateOption>>]
internal sealed class AcbCommandLineHandler : ICommandLineHandler<AcbGenerateOption>
{
    internal const int RelativePathExitCode = -7;
    internal const int GenerationFailedExitCode = -8;

    private readonly IAcbGenerateService acbGenerateService;
    private readonly ICommandLineOutput output;

    public AcbCommandLineHandler(
        IAcbGenerateService acbGenerateService,
        ICommandLineOutput output)
    {
        this.acbGenerateService = acbGenerateService;
        this.output = output;
    }

    public async Task<int> HandleAsync(AcbGenerateOption options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Path.IsPathFullyQualified(options.InputAudioFilePath) ||
            !Path.IsPathFullyQualified(options.OutputFolderPath))
        {
            await output.WriteErrorLineAsync(Lang.CliArgumentNotAbsolutePath);
            return RelativePathExitCode;
        }

        AcbGenerateResult result;
        try
        {
            result = await acbGenerateService.GenerateAsync(options, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            result = new AcbGenerateResult(false, exception.Message);
        }

        if (result.IsSuccess)
            return 0;

        await output.WriteErrorLineAsync($"{Lang.GenerateAudioFileFail} {result.Message}");
        return GenerationFailedExitCode;
    }
}
