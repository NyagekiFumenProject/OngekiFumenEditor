using OngekiFumenEditor.Base.Attributes;

namespace OngekiFumenEditor.Base.OngekiObjects.Projectiles.Attributes
{
    public class BulletPropertyBrowserReadOnlyForPalleteIsValid : ObjectPropertyBrowserReadOnlyForCondition<IBulletPalleteReferencable>
    {
        public BulletPropertyBrowserReadOnlyForPalleteIsValid() :
            base(b => b.ReferenceBulletPallete != null && b.ReferenceBulletPallete != BulletPallete.DummyCustomPallete)
        {
            //bullet's props are editable only when pallete is DummyCustomPallete or null
        }
    }
}
