using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter.Kernel;

namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Convert;

[RegisterSingleton<ICommandLineHandler<FumenConvertOption>>]
internal sealed class ConvertCommandLineHandler : ICommandLineHandler<FumenConvertOption>
{
    internal const int ConversionFailedExitCode = -4;

    private readonly IFumenConvertService convertService;
    private readonly ICommandLineOutput output;

    public ConvertCommandLineHandler(IFumenConvertService convertService, ICommandLineOutput output)
    {
        this.convertService = convertService;
        this.output = output;
    }

    public async Task<int> HandleAsync(FumenConvertOption options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            var result = await convertService.GenerateAsync(options, cancellationToken: cancellationToken);
            if (result.IsSuccess)
                return 0;

            await output.WriteErrorLineAsync($"{Lang.ConvertFail} {result.Message}");
            return ConversionFailedExitCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await output.WriteErrorLineAsync($"{Lang.ConvertFail} {exception.Message}");
            return ConversionFailedExitCode;
        }
    }
}
