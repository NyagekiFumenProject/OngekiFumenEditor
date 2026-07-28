using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels.Interactives.Impls
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

