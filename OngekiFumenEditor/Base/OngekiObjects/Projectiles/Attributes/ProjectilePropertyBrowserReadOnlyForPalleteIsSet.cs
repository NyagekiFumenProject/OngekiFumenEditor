using OngekiFumenEditor.Base.Attributes;

namespace OngekiFumenEditor.Base.OngekiObjects.Projectiles.Attributes
{
    public class ProjectilePropertyBrowserReadOnlyForPalleteIsSet : ObjectPropertyBrowserReadOnlyForCondition<IBulletPalleteReferencable>
    {
        public ProjectilePropertyBrowserReadOnlyForPalleteIsSet() :
            base(b => b.ReferenceBulletPallete is not null)
        {
            // projectile's local props are editable only when palette is unset
        }
    }
}
