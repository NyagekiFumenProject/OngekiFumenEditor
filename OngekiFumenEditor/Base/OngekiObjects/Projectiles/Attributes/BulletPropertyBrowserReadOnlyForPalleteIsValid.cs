using OngekiFumenEditor.Base.Attributes;

namespace OngekiFumenEditor.Base.OngekiObjects.Projectiles.Attributes
{
    public class BulletPropertyBrowserReadOnlyForPalleteIsValid : ObjectPropertyBrowserReadOnlyForCondition<IBulletPalleteReferencable>
    {
        public BulletPropertyBrowserReadOnlyForPalleteIsValid() :
            base(b => b.ReferenceBulletPallete is not null)
        {
            //bullet's local props are editable only when no palette is linked
        }
    }
}
