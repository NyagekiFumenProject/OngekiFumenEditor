using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions
{
	public abstract class EditorAddObjectDropAction : IEditorDropHandler
	{
		protected abstract OngekiObjectBase GetDisplayObject();

		public void Drop(FumenVisualEditorViewModel editor, Point mousePosition)
		{
			var displayObject = GetDisplayObject();
			var isFirst = true;

			if (mousePosition.Y > editor.TotalDurationHeight || mousePosition.Y < 0)
			{
				editor.Toast.ShowMessage(Resource.DisableAddObjectBeyondAudioDuration);
				return;
			}

			editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resource.AddObject, () =>
			{
				editor.MoveObjectTo(displayObject, mousePosition);
				editor.Fumen.AddObject(displayObject);

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
