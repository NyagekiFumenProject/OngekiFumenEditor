using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine;

internal static class AssetBytesAssertions
{
    public static void Write(string filePath, params AssetRecord[] records)
    {
        using var stream = File.Create(filePath);
        using var writer = new BinaryWriter(stream);
        writer.Write(records.Length);
        foreach (var record in records)
        {
            writer.Write(record.Id);
            writer.Write(record.Name);
            writer.Write(record.Dependencies.Length);
            foreach (var dependency in record.Dependencies)
                writer.Write(dependency);
        }
    }

    public static AssetRecord[] Read(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);
        var count = reader.ReadInt32();
        var records = new AssetRecord[count];
        for (var i = 0; i < count; i++)
        {
            var id = reader.ReadInt32();
            var name = reader.ReadString();
            var dependencies = new int[reader.ReadInt32()];
            for (var dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
                dependencies[dependencyIndex] = reader.ReadInt32();
            records[i] = new AssetRecord(id, name, dependencies);
        }

        Assert.Equal(stream.Length, stream.Position);
        return records;
    }

    internal sealed record AssetRecord(int Id, string Name, int[] Dependencies);
}
