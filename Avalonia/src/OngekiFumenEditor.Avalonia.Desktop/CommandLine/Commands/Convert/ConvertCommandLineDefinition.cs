using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.LocalFileSystem;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Convert;

[RegisterSingleton<ICommandLineDefinition>]
internal sealed class ConvertCommandLineDefinition : ICommandLineDefinition
{
    internal const int RelativePathExitCode = -3;

    private readonly ICommandLineHandler<FumenConvertOption> handler;
    private readonly ICommandLineOutput output;

    public ConvertCommandLineDefinition(
        ICommandLineHandler<FumenConvertOption> handler,
        ICommandLineOutput output)
    {
        this.handler = handler;
        this.output = output;
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
        command.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var inputFilePath = parseResult.GetValue(inputFileOption) ?? string.Empty;
            var outputFilePath = parseResult.GetValue(outputFileOption) ?? string.Empty;
            if (!Path.IsPathFullyQualified(inputFilePath) ||
                !Path.IsPathFullyQualified(outputFilePath))
            {
                await output.WriteErrorLineAsync(Lang.CliArgumentNotAbsolutePath);
                return RelativePathExitCode;
            }

            using var inputFile = new LocalSimpleFile(inputFilePath);
            using var outputFile = new LocalSimpleFile(outputFilePath);
            var options = new FumenConvertOption
            {
                InputFumenFile = inputFile,
                OutputFumenFile = outputFile,
                IsStandarizeFumen = parseResult.GetValue(standardizeOption)
            };

            return await handler.HandleAsync(options, cancellationToken);
        });

        return command;
    }
}
