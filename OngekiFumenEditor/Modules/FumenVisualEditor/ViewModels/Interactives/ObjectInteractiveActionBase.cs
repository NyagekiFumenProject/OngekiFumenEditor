using OngekiFumenEditor.Base;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives
{
	public abstract class ObjectInteractiveActionBase
	{
		public abstract void OnDragStart(OngekiObjectBase obj, Point point, FumenVisualEditorViewModel editor);
		public abstract void OnDragMove(OngekiObjectBase obj, Point point, FumenVisualEditorViewModel editor);
		public abstract void OnDragEnd(OngekiObjectBase obj, Point point, FumenVisualEditorViewModel editor);

		public abstract void OnMoveCanvas(OngekiObjectBase obj, Point point, FumenVisualEditorViewModel editor);
	}
}
