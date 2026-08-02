using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.PreviewSvgGenerator;
using OngekiFumenEditor.Avalonia.Parser;

namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Svg;

[RegisterSingleton<ICommandLineHandler<SvgGenerateOption>>]
internal sealed class SvgCommandLineHandler : ICommandLineHandler<SvgGenerateOption>
{
    internal const int RelativePathExitCode = -1;
    internal const int GenerationFailedExitCode = -2;

    private readonly IFumenParserManager parserManager;
    private readonly IPreviewSvgGenerator previewSvgGenerator;
    private readonly IAudioDurationProvider audioDurationProvider;
    private readonly ISvgRasterizer svgRasterizer;
    private readonly ICommandLineOutput output;

    public SvgCommandLineHandler(
        IFumenParserManager parserManager,
        IPreviewSvgGenerator previewSvgGenerator,
        IAudioDurationProvider audioDurationProvider,
        ISvgRasterizer svgRasterizer,
        ICommandLineOutput output)
    {
        this.parserManager = parserManager;
        this.previewSvgGenerator = previewSvgGenerator;
        this.audioDurationProvider = audioDurationProvider;
        this.svgRasterizer = svgRasterizer;
        this.output = output;
    }

    public async Task<int> HandleAsync(SvgGenerateOption options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Path.IsPathFullyQualified(options.InputFumenFilePath) ||
            !Path.IsPathFullyQualified(options.OutputFilePath) ||
            !Path.IsPathFullyQualified(options.AudioFilePath))
        {
            await output.WriteErrorLineAsync(Lang.CliArgumentNotAbsolutePath);
            return RelativePathExitCode;
        }

        try
        {
            await using var fumenFileStream = File.OpenRead(options.InputFumenFilePath);
            var fumenDeserializer = parserManager.GetDeserializer(options.InputFumenFilePath);
            if (fumenDeserializer is null)
                throw new NotSupportedException($"{Lang.DeserializeFumenFileFail}{options.InputFumenFilePath}");

            var fumen = await fumenDeserializer.DeserializeAsync(fumenFileStream);

            // Calculate duration from the audio when available, otherwise from the chart tail.
            options.Duration = File.Exists(options.AudioFilePath)
                ? await audioDurationProvider.GetDurationAsync(options.AudioFilePath, cancellationToken)
                : CalculateDurationFromFumen(fumen);

            if (options.RenderAsPng)
            {
                var svgOptions = CloneWithoutOutputPath(options);
                var svgData = await previewSvgGenerator.GenerateSvgAsync(fumen, svgOptions);
                await svgRasterizer.RasterizeAsync(svgData, options.OutputFilePath, cancellationToken);
            }
            else
            {
                _ = await previewSvgGenerator.GenerateSvgAsync(fumen, options);
            }

            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await output.WriteErrorLineAsync($"{Lang.CallGenerateSvgAsyncFail} {exception.Message}");
            return GenerationFailedExitCode;
        }
    }

    private static TimeSpan CalculateDurationFromFumen(OngekiFumen fumen)
    {
        var maxTGrid = fumen.GetAllDisplayableObjects()
            .OfType<ITimelineObject>()
            .Max(x => x.TGrid);
        maxTGrid += new GridOffset(5, 0);
        return TGridCalculator.ConvertTGridToAudioTime(maxTGrid, fumen.BpmList);
    }

    private static SvgGenerateOption CloneWithoutOutputPath(SvgGenerateOption options) => new()
    {
        InputFumenFilePath = options.InputFumenFilePath,
        OutputFilePath = string.Empty,
        AudioFilePath = options.AudioFilePath,
        XGridDisplayMaxUnit = options.XGridDisplayMaxUnit,
        ViewWidth = options.ViewWidth,
        VerticalScale = options.VerticalScale,
        SoflanMode = options.SoflanMode,
        RenderAsPng = false,
        Duration = options.Duration
    };
}
