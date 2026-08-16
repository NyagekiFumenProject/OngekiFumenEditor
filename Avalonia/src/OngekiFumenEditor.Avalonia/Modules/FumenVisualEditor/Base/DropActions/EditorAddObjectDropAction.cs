using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using Avalonia;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base.DropActions
{
	public abstract class EditorAddObjectDropAction : IEditorDropHandler
	{
		protected abstract OngekiObjectBase GetDisplayObject();

		public void Drop(FumenVisualEditorViewModel editor, Point mousePosition)
		{
			var displayObject = GetDisplayObject();
			if (displayObject is null)
				return;

			var isFirst = true;

            if (!editor.CheckAndNotifyIfPlaceBeyondDuration(mousePosition))
                return;

            editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Lang.B.AddObject.ToLocalizedString(), () =>
			{
				editor.MoveObjectTo(displayObject, mousePosition);
				editor.EditorContext.Fumen.AddObject(displayObject);

				if (isFirst)
				{
					editor.NotifyObjectClicked(displayObject);
					isFirst = false;
				}
			}, () =>
			{
				editor.RemoveObject(displayObject);
			}));
		}
	}
}



