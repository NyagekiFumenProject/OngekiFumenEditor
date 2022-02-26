using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class Bell : OngekiMovableObjectBase, IBulletPalleteReferencable
    {
        public static string CommandName => "BEL";
        public override string IDShortName => CommandName;

        public override Type ModelViewType => typeof(BellViewModel);

        private BulletPalleteAuxiliaryLine line;

        public Bell()
        {
            line = new BulletPalleteAuxiliaryLine(this);
            ReferenceBulletPallete = null;
        }

        private BulletPallete referenceBulletPallete;
        public BulletPallete ReferenceBulletPallete
        {
            get { return referenceBulletPallete; }
            set
            {
                referenceBulletPallete = value;
                NotifyOfPropertyChange(() => ReferenceBulletPallete);
                line.Visibility = value is not null ? Visibility.Visible : Visibility.Hidden;
            }
        }

        public override IEnumerable<IDisplayableObject> GetDisplayableObjects()
        {
            yield return line;
            yield return this;
        }

        public override void Copy(OngekiObjectBase fromObj, OngekiFumen fumen)
        {
            base.Copy(fromObj, fumen);

            if (fromObj is not Bullet from)
                return;

            ReferenceBulletPallete = from.ReferenceBulletPallete;
        }
    }
}
