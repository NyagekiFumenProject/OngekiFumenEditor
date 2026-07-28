using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.FumenConverter.Kernel;

[RegisterSingleton<IFumenConverter>]
public class DefaultFumenConverter : IFumenConverter
{
    public async Task<byte[]> ConvertFumenAsync(OngekiFumen fumen, string savePathOrFormat = "ogkr")
    {
        var parserManager = IoC.Get<IFumenParserManager>();

        if (parserManager.GetSerializer(savePathOrFormat) is not IFumenSerializable serializable)
            throw new FumenConvertException(Lang.OutputFumenNotSupport);

        try
        {
            return await serializable.SerializeAsync(fumen);
        }
        catch (Exception e)
        {
            throw new FumenConvertException($"{Lang.ConvertFail}{e.Message}");
        }
    }
}

public class FumenConvertException : Exception
{
    public FumenConvertException()
    {
    }

    public FumenConvertException(string message) : base(message)
    {
    }

    public FumenConvertException(string message, Exception inner) : base(message, inner)
    {
    }
}


