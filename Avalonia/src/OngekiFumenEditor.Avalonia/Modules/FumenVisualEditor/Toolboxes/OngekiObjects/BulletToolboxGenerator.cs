using Gekimini.Avalonia.Modules.Toolbox.Models;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Toolboxes.OngekiObjects;

[RegisterSingleton<ToolboxItem>]
public class BulletToolboxGenerator : ToolboxGenerator<Bullet>
{
    public BulletToolboxGenerator() : base("Bullet", "Ongeki Objects")
    {
    }

    public override OngekiObjectBase CreateDisplayObject()
    {
        return new Bullet();
    }
}
