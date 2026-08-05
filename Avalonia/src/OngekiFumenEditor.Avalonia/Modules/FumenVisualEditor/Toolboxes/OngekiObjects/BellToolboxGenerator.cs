using Gekimini.Avalonia.Modules.Toolbox.Models;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Toolboxes.OngekiObjects;

[RegisterSingleton<ToolboxItem>]
public class BellToolboxGenerator : ToolboxGenerator<Bell>
{
    public BellToolboxGenerator() : base("Bell", "Ongeki Objects")
    {
    }
}
