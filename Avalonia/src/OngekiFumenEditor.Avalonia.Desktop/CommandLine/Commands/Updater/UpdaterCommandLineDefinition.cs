using Injectio.Attributes;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Updater;

[RegisterSingleton<ICommandLineDefinition>]
internal sealed class UpdaterCommandLineDefinition : ICommandLineDefinition
{
    private readonly ICommandLineHandler<UpdaterOption> handler;

    public UpdaterCommandLineDefinition(ICommandLineHandler<UpdaterOption> handler)
    {
        this.handler = handler;
    }

    public Command CreateCommand()
    {
        var sourceFolderOption = new Option<string>("--sourceFolder")
        {
            Description = "<INTERNAL>",
            Required = true
        };
        var targetFolderOption = new Option<string>("--targetFolder")
        {
            Description = "<INTERNAL>",
            Required = true
        };
        var sourceVersionOption = new Option<string>("--sourceVersion")
        {
            Description = "<INTERNAL>",
            Required = true
        };
        var parentProcessIdOption = new Option<int>("--parentProcessId")
        {
            Description = "<INTERNAL>",
            DefaultValueFactory = _ => 0
        };

        var command = new Command("updater");
        command.Options.Add(sourceFolderOption);
        command.Options.Add(targetFolderOption);
        command.Options.Add(sourceVersionOption);
        command.Options.Add(parentProcessIdOption);
        command.SetAction((ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var options = new UpdaterOption
            {
                SourceFolder = parseResult.GetValue(sourceFolderOption) ?? string.Empty,
                TargetFolder = parseResult.GetValue(targetFolderOption) ?? string.Empty,
                SourceVersion = parseResult.GetValue(sourceVersionOption) ?? string.Empty,
                ParentProcessId = parseResult.GetValue(parentProcessIdOption)
            };

            return handler.HandleAsync(options, cancellationToken);
        });

        return command;
    }
}
