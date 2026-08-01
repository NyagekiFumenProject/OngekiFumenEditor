using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils.Ogkr;

namespace OngekiFumenEditor.Avalonia.Modules.FumenConverter.Kernel;

public static class FumenConverterWrapper
{
    public record GenerateResult(bool IsSuccess, string Message = "");

    public static async Task<GenerateResult> Generate(FumenConvertOption option, OngekiFumen inMemoryFumen = null)
    {
        var parserManager = IoC.Get<IFumenParserManager>();

        OngekiFumen fumen;

        if (inMemoryFumen is null)
        {
            if (string.IsNullOrWhiteSpace(option.InputFumenFilePath))
                return new(false, Lang.NoFumenInput);

            if (parserManager.GetDeserializer(option.InputFumenFilePath) is not { } deserializable)
                return new(false, Lang.FumenFileDeserializeNotSupport);

            await using var inputFile = File.OpenRead(option.InputFumenFilePath);
            fumen = await deserializable.DeserializeAsync(inputFile);
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

            var res = await StandardizeFormat.Process(fumen);
            if (!res.IsSuccess)
                return new(false, res.Message);

            fumen = res.SerializedFumen;
        }

        var converter = IoC.Get<IFumenConverter>();
        try
        {
            var output = await converter.ConvertFumenAsync(fumen, option.OutputFumenFilePath);
            await using var outfile = File.Create(option.OutputFumenFilePath);
            await outfile.WriteAsync(output);
        }
        catch (FumenConvertException e)
        {
            return new(false, e.Message);
        }

        return new(true);
    }
}
