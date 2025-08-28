using OngekiFumenEditor.Base.Attributes;

namespace OngekiFumenEditor.Base.OngekiObjects.Projectiles.Attributes
{
    public class PropertyBrowserReadOnlyForPalleteIsValid : ObjectPropertyBrowserReadOnlyForCondition<IBulletPalleteReferencable>
    {
        public PropertyBrowserReadOnlyForPalleteIsValid() :
            base(b => b.ReferenceBulletPallete != null && b.ReferenceBulletPallete != BulletPallete.DummyCustomPallete)
        {
        }
    }
}
