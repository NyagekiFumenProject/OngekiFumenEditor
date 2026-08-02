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
            if (string.IsNullOrWhiteSpace(option.InputFumenFilePath))
                return new(false, Lang.NoFumenInput);

            if (parserManager.GetDeserializer(option.InputFumenFilePath) is not { } deserializable)
                return new(false, Lang.FumenFileDeserializeNotSupport);

            await using var inputFile = File.OpenRead(option.InputFumenFilePath);
            fumen = await deserializable.DeserializeAsync(inputFile);
            cancellationToken.ThrowIfCancellationRequested();
        }
        else
        {
            fumen = inMemoryFumen;
        }

        if (string.IsNullOrWhiteSpace(option.OutputFumenFilePath))
            return new(false, Lang.OutputFumenFileNotSelect);

        if (option.IsStandarizeFumen)
        {
            if (!string.Equals(Path.GetExtension(option.OutputFumenFilePath), ".ogkr",
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
            var output = await converter.ConvertFumenAsync(fumen, option.OutputFumenFilePath);
            await WriteAtomicallyAsync(option.OutputFumenFilePath, output, cancellationToken);
        }
        catch (FumenConvertException exception)
        {
            return new(false, exception.Message);
        }

        return new(true);
    }

    private static async Task WriteAtomicallyAsync(
        string outputPath,
        byte[] output,
        CancellationToken cancellationToken)
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        var outputDirectory = Path.GetDirectoryName(fullOutputPath)
            ?? throw new IOException($"Unable to determine the output directory for '{outputPath}'.");
        var temporaryPath = Path.Combine(
            outputDirectory,
            $".{Path.GetFileName(fullOutputPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllBytesAsync(temporaryPath, output, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, fullOutputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
