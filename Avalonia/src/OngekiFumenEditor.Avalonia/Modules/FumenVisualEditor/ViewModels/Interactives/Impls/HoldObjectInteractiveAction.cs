using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using System.Collections.Generic;
using Avalonia;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels.Interactives.Impls
{
	public class HoldObjectInteractiveAction : DockableObjectInteractiveAction
	{
		public override IEnumerable<ConnectableObjectBase> PickDockableObjects(FumenVisualEditorViewModel editor = null)
		{
			return base.PickDockableObjects(editor);
		}

		public override void OnMoveCanvas(OngekiObjectBase obj, Point relativePoint, FumenVisualEditorViewModel editor)
		{
			base.OnMoveCanvas(obj, relativePoint, editor);

			UpdateHoldEndXGrid(obj);
		}

		public override void OnDragEnd(OngekiObjectBase obj, Point point, FumenVisualEditorViewModel editor)
		{
			base.OnDragEnd(obj, point, editor);

			editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Lang.B.UpdateHoldEndPosition.ToLocalizedString(),
				() =>
				{
					//UpdateHoldEndXGrid(obj);
				}, () =>
				{
					UpdateHoldEndXGrid(obj);
				}));
		}

		private void UpdateHoldEndXGrid(OngekiObjectBase obj)
		{
			var hold = obj as Hold;
			if (hold.HoldEnd is HoldEnd end && hold.ReferenceLaneStart is ConnectableStartObject start)
			{
				if (start.CalulateXGrid(end.TGrid) is XGrid xGrid)
					end.XGrid = xGrid;
			}
		}

	}
}



