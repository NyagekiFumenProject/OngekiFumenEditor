using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Acb;

[RegisterSingleton<ICommandLineDefinition>]
internal sealed class AcbCommandLineDefinition : ICommandLineDefinition
{
    private readonly ICommandLineHandler<AcbGenerateOption> handler;

    public AcbCommandLineDefinition(ICommandLineHandler<AcbGenerateOption> handler)
    {
        this.handler = handler;
    }

    public Command CreateCommand()
    {
        var musicIdOption = new Option<int>("--musicId")
        {
            Description = Lang.ProgramOptionMusicId,
            Required = true
        };
        var inputFileOption = new Option<string>("--inputFile")
        {
            Description = Lang.ProgramOptionInputFileAudio,
            Required = true
        };
        var outputFolderOption = new Option<string>("--outputFolder")
        {
            Description = Lang.ProgramOptionOutputFolder,
            Required = true
        };
        var previewBeginOption = new Option<int>("--previewBegin")
        {
            Description = Lang.ProgramOptionPreviewBegin,
            DefaultValueFactory = _ => 60000
        };
        var previewEndOption = new Option<int>("--previewEnd")
        {
            Description = Lang.ProgramOptionPreviewEnd,
            DefaultValueFactory = _ => 80000
        };

        var command = new Command("acb", Lang.ProgramCommandAcb);
        command.Options.Add(musicIdOption);
        command.Options.Add(inputFileOption);
        command.Options.Add(outputFolderOption);
        command.Options.Add(previewBeginOption);
        command.Options.Add(previewEndOption);
        command.SetAction((ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var options = new AcbGenerateOption
            {
                MusicId = parseResult.GetValue(musicIdOption),
                InputAudioFilePath = parseResult.GetValue(inputFileOption) ?? string.Empty,
                OutputFolderPath = parseResult.GetValue(outputFolderOption) ?? string.Empty,
                PreviewBeginTime = parseResult.GetValue(previewBeginOption),
                PreviewEndTime = parseResult.GetValue(previewEndOption)
            };

            return handler.HandleAsync(options, cancellationToken);
        });

        return command;
    }
}
