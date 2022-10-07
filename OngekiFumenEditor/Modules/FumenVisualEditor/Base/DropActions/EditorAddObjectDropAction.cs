using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("添加物件", () =>
            {
                editor.Fumen.AddObject(displayObject);
                editor.MoveObjectTo(displayObject, mousePosition);
                editor.Redraw(RedrawTarget.OngekiObjects);

                if (isFirst)
                {
                    editor.NotifyObjectClicked(displayObject);
                    isFirst = false;
                }
            }, () =>
            {
                editor.RemoveObject(displayObject);
                editor.Redraw(RedrawTarget.OngekiObjects);
            }));
        }
    }
}
