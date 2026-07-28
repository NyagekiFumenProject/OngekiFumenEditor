using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Parser;

public interface IFumenParserManager
{
    IFumenSerializable GetSerializer(string saveFilePath);
    IFumenDeserializable GetDeserializer(string loadFilePath);

    IEnumerable<(string desc, string[] fileFormat)> GetSerializerDescriptions();
    IEnumerable<(string desc, string[] fileFormat)> GetDeserializerDescriptions();

    async Task Serialize(OngekiFumen fumen, string saveFilePath)
    {
        await File.WriteAllBytesAsync(saveFilePath, await GetSerializer(saveFilePath).SerializeAsync(fumen));
    }

    async Task<OngekiFumen> Deserialize(string loadFilePath)
    {
        await using var fs = File.OpenRead(loadFilePath);
        return await GetDeserializer(loadFilePath).DeserializeAsync(fs);
    }
}

