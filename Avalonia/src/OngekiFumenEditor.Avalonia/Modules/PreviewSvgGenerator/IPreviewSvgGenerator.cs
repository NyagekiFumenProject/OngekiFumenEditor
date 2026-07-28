using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Modules.PreviewSvgGenerator;

public interface IPreviewSvgGenerator
{
    Task<byte[]> GenerateSvgAsync(OngekiFumen fumen, SvgGenerateOption option);
}

