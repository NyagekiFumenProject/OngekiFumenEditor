#nullable enable

using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.FumenConverter;

public class FumenConvertOption
{
    public ISimpleFile? InputFumenFile { get; set; }

    public ISimpleFile? OutputFumenFile { get; set; }

    public bool IsStandarizeFumen { get; set; } = false;
}

