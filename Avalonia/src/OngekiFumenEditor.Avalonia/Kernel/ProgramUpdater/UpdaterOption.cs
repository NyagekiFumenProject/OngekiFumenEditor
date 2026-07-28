using OngekiFumenEditor.Avalonia.Kernel.CommandExecutor.Attributes;

namespace OngekiFumenEditor.Avalonia.Kernel.ProgramUpdater;

public class UpdaterOption
{
    [OptionBindingAttrbute<string>("sourceFolder", "<INTERNAL>", null, Require = true)]
    public string SourceFolder { get; set; }

    [OptionBindingAttrbute<string>("targetFolder", "<INTERNAL>", null, Require = true)]
    public string TargetFolder { get; set; }

    [OptionBindingAttrbute<string>("sourceVersion", "<INTERNAL>", null, Require = true)]
    public string SourceVersion { get; set; }
}

