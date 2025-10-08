using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Toolboxes.OngekiObjects
{
    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Bullet", "Ongeki Objects")]
    public class BulletToolboxGenerator : ToolboxGenerator<Bullet>
    {
        public override OngekiObjectBase CreateDisplayObject()
        {
            var bullet = base.CreateDisplayObject() as Bullet;
            bullet.ReferenceBulletPallete = BulletPallete.DummyCustomPallete;
            return bullet;
        }
    }
}
