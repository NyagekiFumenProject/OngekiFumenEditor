using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls
{
    public class HoldObjectInteractiveAction : DockableObjectInteractiveAction
    {
        public override IEnumerable<ConnectableObjectBase> PickDockableObjects(FumenVisualEditorViewModel editor = null)
        {
            return base.PickDockableObjects(editor)
                    .Where(x => x.IDShortName[0] == 'L');
        }

        public override double? CheckAndAdjustX(double x, FumenVisualEditorViewModel editor)
        {
            /*
            if (((ILaneDockable)obj).ReferenceLaneStart is ConnectableStartObject start)
                return x;
            */
            return x;
        }
    }
}
