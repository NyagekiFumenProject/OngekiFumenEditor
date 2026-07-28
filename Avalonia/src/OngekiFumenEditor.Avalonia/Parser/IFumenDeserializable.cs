using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Parser;

public interface IFumenDeserializable
{
    string FileFormatName { get; }
    string[] SupportFumenFileExtensions { get; }
    Task<OngekiFumen> DeserializeAsync(Stream stream);
}

