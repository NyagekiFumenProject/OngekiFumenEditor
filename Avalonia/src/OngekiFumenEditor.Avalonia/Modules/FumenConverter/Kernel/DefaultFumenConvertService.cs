using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Utils.Ogkr;

namespace OngekiFumenEditor.Avalonia.Modules.FumenConverter.Kernel;

[RegisterSingleton<IFumenConvertService>]
public sealed class DefaultFumenConvertService : IFumenConvertService
{
    private readonly IFumenParserManager parserManager;
    private readonly IFumenConverter converter;
    private readonly IReadOnlyList<IFumenCheckRule> checkRules;

    public DefaultFumenConvertService(
        IFumenParserManager parserManager,
        IFumenConverter converter,
        IEnumerable<IFumenCheckRule> checkRules)
    {
        this.parserManager = parserManager;
        this.converter = converter;
        this.checkRules = checkRules.ToArray();
    }

    public async Task<FumenConverterWrapper.GenerateResult> GenerateAsync(
        FumenConvertOption option,
        OngekiFumen inMemoryFumen = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(option);
        cancellationToken.ThrowIfCancellationRequested();

        OngekiFumen fumen;
        if (inMemoryFumen is null)
        {
            if (option.InputFumenFile is not { } input)
                return new(false, Lang.NoFumenInput);

            var inputFileName = input.FileName;
            if (string.IsNullOrWhiteSpace(inputFileName))
                return new(false, Lang.NoFumenInput);

            if (parserManager.GetDeserializer(inputFileName) is not { } deserializable)
                return new(false, Lang.FumenFileDeserializeNotSupport);

            await using var inputStream = await input.OpenReadAsync(cancellationToken);
            fumen = await deserializable.DeserializeAsync(inputStream);
            cancellationToken.ThrowIfCancellationRequested();
        }
        else
        {
            fumen = inMemoryFumen;
        }

        if (option.OutputFumenFile is not { } target)
            return new(false, Lang.OutputFumenFileNotSelect);

        var outputFileName = target.FileName;
        if (string.IsNullOrWhiteSpace(outputFileName))
            return new(false, Lang.OutputFumenFileNotSelect);

        if (option.IsStandarizeFumen)
        {
            if (!string.Equals(Path.GetExtension(outputFileName), ".ogkr",
                    StringComparison.OrdinalIgnoreCase))
                return new(false, Lang.OutputFumenStandardizeFormatNotSupported);

            var result = await StandardizeFormat.Process(fumen, parserManager, checkRules);
            if (!result.IsSuccess)
                return new(false, result.Message);

            fumen = result.SerializedFumen;
            cancellationToken.ThrowIfCancellationRequested();
        }

        try
        {
            var output = await converter.ConvertFumenAsync(fumen, outputFileName);
            await target.WriteAllBytesAsync(output, cancellationToken);
        }
        catch (FumenConvertException exception)
        {
            return new(false, exception.Message);
        }

        return new(true);
    }
}
