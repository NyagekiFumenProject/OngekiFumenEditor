using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Parser;

public interface IFumenSerializable
{
    string FileFormatName { get; }
    string[] SupportFumenFileExtensions { get; }
    Task<byte[]> SerializeAsync(OngekiFumen fumen);
}

