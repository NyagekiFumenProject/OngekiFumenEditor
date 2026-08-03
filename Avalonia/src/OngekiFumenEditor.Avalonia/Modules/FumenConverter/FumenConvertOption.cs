using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.FumenConverter;

public class FumenConvertOption
{
    public string InputFumenFilePath { get; set; }

    public string OutputFumenFilePath { get; set; }

    public ISimpleFile InputFumenFile { get; set; }

    public ISimpleFile OutputFumenFile { get; set; }

    public bool IsStandarizeFumen { get; set; } = false;
}

