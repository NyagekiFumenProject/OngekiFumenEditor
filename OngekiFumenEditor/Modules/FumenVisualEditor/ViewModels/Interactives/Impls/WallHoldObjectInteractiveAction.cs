using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls
{
    public class WallHoldObjectInteractiveAction : DockableObjectInteractiveAction
    {
        public override IEnumerable<ConnectableObjectBase> PickDockableObjects(FumenVisualEditorViewModel editor = null)
        {
            return base.PickDockableObjects(editor)
                    .Where(x => x.IDShortName[0] == 'W');
        }
    }
}
