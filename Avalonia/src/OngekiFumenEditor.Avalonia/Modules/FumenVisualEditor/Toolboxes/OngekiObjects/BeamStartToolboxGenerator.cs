using Gekimini.Avalonia.Modules.Toolbox.Models;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Toolboxes.OngekiObjects;

[RegisterSingleton<ToolboxItem>]
public class BeamStartToolboxGenerator : ToolboxGenerator<BeamStart>
{
    public BeamStartToolboxGenerator() : base("Beam Start", "Ongeki Objects")
    {
    }
}
