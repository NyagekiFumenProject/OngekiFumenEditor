using OngekiFumenEditor.Base.Attributes;

namespace OngekiFumenEditor.Base.OngekiObjects.Projectiles.Attributes
{
    public class BellPropertyBrowserReadOnlyForPalleteIsNotDummyCustom : ObjectPropertyBrowserReadOnlyForCondition<IBulletPalleteReferencable>
    {
        public BellPropertyBrowserReadOnlyForPalleteIsNotDummyCustom() :
            base(b => b.ReferenceBulletPallete != BulletPallete.DummyCustomPallete)
        {
            //bell's pallete can be null, so props are editable only when pallete is DummyCustomPallete
        }
    }
}
