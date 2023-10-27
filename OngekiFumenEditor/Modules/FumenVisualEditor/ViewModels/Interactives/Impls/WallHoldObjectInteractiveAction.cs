using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using System.Collections.Generic;
using System.Linq;

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
