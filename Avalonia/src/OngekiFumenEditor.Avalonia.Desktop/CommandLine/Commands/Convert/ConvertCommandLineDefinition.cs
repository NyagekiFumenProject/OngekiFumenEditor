using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Convert;

[RegisterSingleton<ICommandLineDefinition>]
internal sealed class ConvertCommandLineDefinition : ICommandLineDefinition
{
    private readonly ICommandLineHandler<FumenConvertOption> handler;

    public ConvertCommandLineDefinition(ICommandLineHandler<FumenConvertOption> handler)
    {
        this.handler = handler;
    }

    public Command CreateCommand()
    {
        var inputFileOption = new Option<string>("--inputFile")
        {
            Description = Lang.ProgramOptionInputFile,
            Required = true
        };
        var outputFileOption = new Option<string>("--outputFile")
        {
            Description = Lang.ProgramOptionOutputFile,
            Required = true
        };
        var standardizeOption = new Option<bool>("--standardize")
        {
            Description = Lang.ProgramOptionStandardizeFumen
        };

        var command = new Command("convert", Lang.ProgramCommandConvert);
        command.Options.Add(inputFileOption);
        command.Options.Add(outputFileOption);
        command.Options.Add(standardizeOption);
        command.SetAction((ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var options = new FumenConvertOption
            {
                InputFumenFilePath = parseResult.GetValue(inputFileOption) ?? string.Empty,
                OutputFumenFilePath = parseResult.GetValue(outputFileOption) ?? string.Empty,
                IsStandarizeFumen = parseResult.GetValue(standardizeOption)
            };

            return handler.HandleAsync(options, cancellationToken);
        });

        return command;
    }
}
