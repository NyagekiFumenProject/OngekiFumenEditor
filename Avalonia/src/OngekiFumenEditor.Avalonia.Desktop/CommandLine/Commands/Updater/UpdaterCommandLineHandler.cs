using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Updater;

[RegisterSingleton<ICommandLineHandler<UpdaterOption>>]
internal sealed class UpdaterCommandLineHandler : ICommandLineHandler<UpdaterOption>
{
    private readonly IProgramUpdateService programUpdateService;
    private readonly ICommandLineOutput output;

    public UpdaterCommandLineHandler(
        IProgramUpdateService programUpdateService,
        ICommandLineOutput output)
    {
        this.programUpdateService = programUpdateService;
        this.output = output;
    }

    public async Task<int> HandleAsync(UpdaterOption options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await programUpdateService.UpdateAsync(options, cancellationToken);
        if (result.ExitCode != 0)
            await output.WriteErrorLineAsync(result.Message);
        return result.ExitCode;
    }
}
