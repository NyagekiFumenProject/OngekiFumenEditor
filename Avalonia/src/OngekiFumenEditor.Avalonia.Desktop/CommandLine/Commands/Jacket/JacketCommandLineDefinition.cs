using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Jacket;

[RegisterSingleton<ICommandLineDefinition>]
internal sealed class JacketCommandLineDefinition : ICommandLineDefinition
{
    private readonly ICommandLineHandler<JacketGenerateOption> handler;

    public JacketCommandLineDefinition(ICommandLineHandler<JacketGenerateOption> handler)
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
        var outputFolderOption = new Option<string>("--outputFolder")
        {
            Description = Lang.ProgramOptionOutputFolderAsset,
            Required = true
        };
        var inputFileOption = new Option<string>("--inputFile")
        {
            Description = Lang.ProgramOptionInputFileJacket,
            Required = true
        };
        var outputWidthOption = new Option<int>("--outputWidth")
        {
            Description = Lang.ProgramOptionJacketOutputWidth,
            DefaultValueFactory = _ => 520
        };
        var outputHeightOption = new Option<int>("--outputHeight")
        {
            Description = Lang.ProgramOptionJacketOutputHeight,
            DefaultValueFactory = _ => 520
        };
        var outputWidthSmallOption = new Option<int>("--outputWidthSmall")
        {
            Description = Lang.ProgramOptionJacketOutputWidthSmall,
            DefaultValueFactory = _ => 220
        };
        var outputHeightSmallOption = new Option<int>("--outputHeightSmall")
        {
            Description = Lang.ProgramOptionJacketOutputHeightSmall,
            DefaultValueFactory = _ => 220
        };
        var updateAssetBytesFileOption = new Option<bool>("--updateAssetBytesFile")
        {
            Description = Lang.UpdateAssetBytesFile,
            DefaultValueFactory = _ => true
        };

        var command = new Command("jacket", Lang.ProgramCommandJacket);
        command.Options.Add(musicIdOption);
        command.Options.Add(outputFolderOption);
        command.Options.Add(inputFileOption);
        command.Options.Add(outputWidthOption);
        command.Options.Add(outputHeightOption);
        command.Options.Add(outputWidthSmallOption);
        command.Options.Add(outputHeightSmallOption);
        command.Options.Add(updateAssetBytesFileOption);
        command.SetAction((ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var options = new JacketGenerateOption
            {
                MusicId = parseResult.GetValue(musicIdOption),
                OutputAssetbundleFolderPath = parseResult.GetValue(outputFolderOption) ?? string.Empty,
                InputImageFilePath = parseResult.GetValue(inputFileOption) ?? string.Empty,
                Width = parseResult.GetValue(outputWidthOption),
                Height = parseResult.GetValue(outputHeightOption),
                WidthSmall = parseResult.GetValue(outputWidthSmallOption),
                HeightSmall = parseResult.GetValue(outputHeightSmallOption),
                UpdateAssetBytesFile = parseResult.GetValue(updateAssetBytesFileOption)
            };

            return handler.HandleAsync(options, cancellationToken);
        });

        return command;
    }
}
