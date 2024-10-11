using OngekiFumenEditor.Kernel.ArgProcesser.Attributes;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Modules.FumenConverter;

public class FumenConvertOption
{
    [LocalizableOptionBinding<string>("inputFile", nameof(Resources.ProgramOptionInputFile), default, Require = true)]
    public string InputFumenFilePath { get; set; }

    [LocalizableOptionBinding<string>("outputFile", nameof(Resources.ProgramOptionOutputFile), default, Require = true)]
    public string OutputFumenFilePath { get; set; }

    [LocalizableOptionBinding<bool>("standardize", nameof(Resources.ProgramOptionStandardizeFumen), default)]
    public bool IsStandarizeFumen { get; set; } = false;
}