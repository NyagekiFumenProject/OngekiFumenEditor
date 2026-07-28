using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Modules.FumenConverter;

public interface IFumenConverter
{
    Task<byte[]> ConvertFumenAsync(OngekiFumen fumen, string savePathOrFormat);
}

