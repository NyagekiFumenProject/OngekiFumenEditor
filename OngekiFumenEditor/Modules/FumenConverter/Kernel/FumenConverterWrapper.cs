using System.IO;
using System.Threading.Tasks;
using Caliburn.Micro;
using Microsoft.CodeAnalysis.Options;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Base;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Parser.DefaultImpl;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils.Ogkr;

namespace OngekiFumenEditor.Modules.FumenConverter.Kernel;

public static class FumenConverterWrapper
{
    public static async Task<GenerateResult> Generate(FumenConvertOption option, OngekiFumen inMemoryFumen = null)
    {
        var parserManager = IoC.Get<IFumenParserManager>();

        OngekiFumen fumen;

        if (inMemoryFumen is null) {
            if (parserManager.GetDeserializer(option.InputFumenFilePath) is not IFumenDeserializable deserializable) {
                return new(false, Resources.FumenFileDeserializeNotSupport);
            }

            if (string.IsNullOrWhiteSpace(option.InputFumenFilePath))
                return new(false, Resources.NoFumenInput);

            fumen = await deserializable.DeserializeAsync(File.OpenRead(option.InputFumenFilePath));
        }
        else {
            fumen = inMemoryFumen;
        }

        if (string.IsNullOrWhiteSpace(option.OutputFumenFilePath)) 
            return new(false, Resources.OutputFumenFileNotSelect);

        if (option.IsStandarizeFumen) {
            if (!option.OutputFumenFilePath.EndsWith(".ogkr")) {
                return new(false, Resources.OutputFumenStandardizeFormatNotSupported);
            }

            var res = await StandardizeFormat.Process(fumen);
            if (!res.IsSuccess) {
                return new(false, res.Message);
            }

            fumen = res.SerializedFumen;
        }
        
        var converter = IoC.Get<IFumenConverter>();
        try {
            var output = await converter.ConvertFumenAsync(fumen, option.OutputFumenFilePath);
            await using var outfile = File.OpenWrite(option.OutputFumenFilePath);
            await outfile.WriteAsync(output);
        }
        catch (FumenConvertException e) {
            return new(false, e.Message);
        }

        return new GenerateResult(true);
    }
}