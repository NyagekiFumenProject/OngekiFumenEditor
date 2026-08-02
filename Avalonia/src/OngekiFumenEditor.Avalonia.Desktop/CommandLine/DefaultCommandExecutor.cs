using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.Logs;
using System.CommandLine;

namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine;

[RegisterSingleton<ICommandExecutor>]
internal sealed class DefaultCommandExecutor : ICommandExecutor
{
    private readonly RootCommand rootCommand;
    private readonly Option<bool> verbosityOption;

    internal RootCommand RootCommand => rootCommand;

    public DefaultCommandExecutor(IEnumerable<ICommandLineDefinition> definitions)
    {
        rootCommand = new RootCommand("CommandLine for OngekiFumenEditor");

        verbosityOption = new Option<bool>("--verbose", "-v")
        {
            Description = Lang.ProgramOptionDescriptionVerbose,
            Recursive = true
        };
        rootCommand.Options.Add(verbosityOption);

        var commandNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            var command = definition.CreateCommand()
                ?? throw new InvalidOperationException(
                    $"Command-line definition '{definition.GetType().FullName}' returned no command.");

            if (!commandNames.Add(command.Name))
                throw new InvalidOperationException($"Duplicate command-line command name '{command.Name}'.");

            rootCommand.Subcommands.Add(command);
        }
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        var parseResult = rootCommand.Parse(args);
        var isVerbose = parseResult.GetValue(verbosityOption);
        var logOutputs = isVerbose
            ? new ILogOutput[] { new CommandLineLogOutput() }
            : [];
        Log.Initialize(new Log(logOutputs));
        var exitCode = await parseResult.InvokeAsync(cancellationToken: cancellationToken);
        if (isVerbose)
            await Log.WaitForAllLogWriteDone();
        return exitCode;
    }
}
