using OngekiFumenEditor.Base.Attributes;

namespace OngekiFumenEditor.Base.OngekiObjects.Projectiles.Attributes
{
    public class PropertyBrowserReadOnlyForPalleteNotNull : ObjectPropertyBrowserReadOnlyForCondition<IBulletPalleteReferencable>
    {
        public PropertyBrowserReadOnlyForPalleteNotNull() : base(b => b.ReferenceBulletPallete != null)
        {
        }
    }
}
