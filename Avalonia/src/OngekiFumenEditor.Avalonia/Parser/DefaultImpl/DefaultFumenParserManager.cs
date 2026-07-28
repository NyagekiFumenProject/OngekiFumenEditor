using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Parser.DefaultImpl;

[RegisterSingleton<IFumenParserManager>]
public class DefaultFumenParserManager : IFumenParserManager
{
    public List<IFumenSerializable> FumenSerializers { get; }
    public List<IFumenDeserializable> FumenDeserializers { get; }

    public DefaultFumenParserManager(IEnumerable<IFumenSerializable> fumenSerializers,
        IEnumerable<IFumenDeserializable> fumenDeserializers)
    {
        FumenSerializers = fumenSerializers.ToList();
        FumenDeserializers = fumenDeserializers.ToList();
    }

    public IFumenSerializable GetSerializer(string saveFilePath)
    {
        return FumenSerializers.FirstOrDefault(x =>
            x.SupportFumenFileExtensions.Any(y => saveFilePath.EndsWith(y, StringComparison.InvariantCultureIgnoreCase)));
    }

    public IFumenDeserializable GetDeserializer(string loadFilePath)
    {
        return FumenDeserializers.FirstOrDefault(x =>
            x.SupportFumenFileExtensions.Any(y => loadFilePath.EndsWith(y, StringComparison.InvariantCultureIgnoreCase)));
    }

    public IEnumerable<(string desc, string[] fileFormat)> GetSerializerDescriptions() =>
        FumenSerializers.Select(x => (x.FileFormatName, x.SupportFumenFileExtensions));

    public IEnumerable<(string desc, string[] fileFormat)> GetDeserializerDescriptions() =>
        FumenDeserializers.Select(x => (x.FileFormatName, x.SupportFumenFileExtensions));
}

