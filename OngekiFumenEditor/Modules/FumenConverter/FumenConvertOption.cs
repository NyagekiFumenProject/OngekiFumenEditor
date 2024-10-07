using OngekiFumenEditor.Kernel.ArgProcesser.Attributes;

namespace OngekiFumenEditor.Modules.FumenConverter;

public class FumenConvertOption
{
    [LocalizableOptionBinding<string>("inputFile", "ProgramOptionInputFile", default, Require = true)]
    public string InputFumenFilePath { get; set; }
    
    [LocalizableOptionBinding<string>("outputFile", "ProgramOptionOutputFile", default, Require = true)]
    public string OutputFumenFilePath { get; set; }
}