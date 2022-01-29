using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.Attrbutes;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    //[DontShowPropertyInfoAttrbute]
    public class Bullet : OngekiMovableObjectBase
    {
        private BulletPallete referenceBulletPallete;
        public BulletPallete ReferenceBulletPallete
        {
            get { return referenceBulletPallete; }
            set
            {
                referenceBulletPallete = value;
                NotifyOfPropertyChange(() => ReferenceBulletPallete);
            }
        }

        private BulletAuxiliaryLine line;

        public Bullet()
        {
            line = new BulletAuxiliaryLine(this);
        }

        public override IEnumerable<IDisplayableObject> GetDisplayableObjects()
        {
            yield return line;
            yield return this;
        }

        public override Type ModelViewType => typeof(BulletViewModel);

        public override string IDShortName => CommandName;

        public const string CommandName = "BLT";

        public override string ToString() => $"{base.ToString()} Pallete:({ReferenceBulletPallete})";

        public override void Copy(OngekiObjectBase fromObj, OngekiFumen fumen)
        {
            base.Copy(fromObj, fumen);

            if (fromObj is not Bullet from)
                return;

            ReferenceBulletPallete = from.ReferenceBulletPallete;
        }
    }
}
