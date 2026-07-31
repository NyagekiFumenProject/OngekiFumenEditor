using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Toolboxes.OngekiObjects;

[RegisterTransient<IToolboxGenerator>]
public class BulletToolboxGenerator : ToolboxGenerator<Bullet>
{
    public override OngekiObjectBase CreateDisplayObject()
    {
        var bullet = (Bullet)base.CreateDisplayObject();
        bullet.ReferenceBulletPallete = BulletPallete.DummyCustomPallete;
        return bullet;
    }
}
