using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Modules.PreviewSvgGenerator;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Svg;

[RegisterSingleton<ICommandLineDefinition>]
internal sealed class SvgCommandLineDefinition : ICommandLineDefinition
{
    private readonly ICommandLineHandler<SvgGenerateOption> handler;

    public SvgCommandLineDefinition(ICommandLineHandler<SvgGenerateOption> handler)
    {
        this.handler = handler;
    }

    public Command CreateCommand()
    {
        var inputFileOption = new Option<string>("--inputFile")
        {
            Description = Lang.ProgramOptionInputFileNyageki,
            Required = true
        };
        var outputFileOption = new Option<string>("--outputFile")
        {
            Description = Lang.ProgramOptionOutputFile,
            Required = true
        };
        var audioFileOption = new Option<string>("--audioFile")
        {
            Description = Lang.ProgramOptionInputFileAudio,
            Required = true
        };
        var maxXGridOption = new Option<double>("--maxXGrid")
        {
            Description = Lang.ProgramOptionSvgMaxXGrid,
            DefaultValueFactory = _ => 40
        };
        var viewWidthOption = new Option<double>("--viewWidth")
        {
            Description = Lang.ProgramOptionSvgViewWidth,
            DefaultValueFactory = _ => 800
        };
        var verticalScaleOption = new Option<double>("--verticalScale")
        {
            Description = Lang.ProgramOptionSvgVerticalScale,
            DefaultValueFactory = _ => 1
        };
        var soflanModeOption = new Option<SoflanMode>("--soflanMode")
        {
            Description = Lang.ProgramOptionSvgSoflanMode,
            DefaultValueFactory = _ => SoflanMode.Soflan
        };
        var pngOption = new Option<bool>("--png")
        {
            Description = Lang.ProgramOptionSvgRenderAsPng,
            DefaultValueFactory = _ => false
        };

        var command = new Command("svg", Lang.ProgramCommandDescriptionSvg);
        command.Options.Add(inputFileOption);
        command.Options.Add(outputFileOption);
        command.Options.Add(audioFileOption);
        command.Options.Add(maxXGridOption);
        command.Options.Add(viewWidthOption);
        command.Options.Add(verticalScaleOption);
        command.Options.Add(soflanModeOption);
        command.Options.Add(pngOption);
        command.SetAction((ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var options = new SvgGenerateOption
            {
                InputFumenFilePath = parseResult.GetValue(inputFileOption) ?? string.Empty,
                OutputFilePath = parseResult.GetValue(outputFileOption) ?? string.Empty,
                AudioFilePath = parseResult.GetValue(audioFileOption) ?? string.Empty,
                XGridDisplayMaxUnit = parseResult.GetValue(maxXGridOption),
                ViewWidth = parseResult.GetValue(viewWidthOption),
                VerticalScale = parseResult.GetValue(verticalScaleOption),
                SoflanMode = parseResult.GetValue(soflanModeOption),
                RenderAsPng = parseResult.GetValue(pngOption)
            };

            return handler.HandleAsync(options, cancellationToken);
        });

        return command;
    }
}
