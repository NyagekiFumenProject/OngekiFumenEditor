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

            if (mousePosition.Y > editor.TotalDurationHeight || mousePosition.Y < 0)
            {
                editor.Toast.ShowMessage("无法添加物件到音频范围之外");
                return;
            }

            editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("添加物件", () =>
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
