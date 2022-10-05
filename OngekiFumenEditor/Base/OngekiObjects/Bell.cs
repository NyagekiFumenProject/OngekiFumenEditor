using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class Bell : OngekiMovableObjectBase, IBulletPalleteReferencable
    {
        public static string CommandName => "BEL";
        public override string IDShortName => CommandName;

        public Bell()
        {
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
            }
        }

        public override IEnumerable<IDisplayableObject> GetDisplayableObjects()
        {
            yield return this;
        }

        public override void Copy(OngekiObjectBase fromObj, OngekiFumen fumen)
        {
            base.Copy(fromObj, fumen);

            if (fromObj is not Bell from)
                return;

            ReferenceBulletPallete = from.ReferenceBulletPallete;
        }
    }
}
