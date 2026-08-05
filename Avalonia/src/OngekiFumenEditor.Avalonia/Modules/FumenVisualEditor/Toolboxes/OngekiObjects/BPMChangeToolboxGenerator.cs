using Gekimini.Avalonia.Modules.Toolbox.Models;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Toolboxes.OngekiObjects;

[RegisterSingleton<ToolboxItem>]
public class BPMChangeToolboxGenerator : ToolboxGenerator<BPMChange>
{
    public BPMChangeToolboxGenerator() : base("BPM Change", "Ongeki Objects")
    {
    }
}
