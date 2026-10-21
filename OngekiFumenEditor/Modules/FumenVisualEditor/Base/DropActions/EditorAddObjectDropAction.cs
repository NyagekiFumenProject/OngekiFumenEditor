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
            if (!editor.CheckAndNotifyIfPlaceBeyondDuration(mousePosition))
                return;

            var displayObject = GetDisplayObject();
            var isFirst = true;

            editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.AddObject, () =>
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
