using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels.DropActions
{
    public class ConnectableObjectDropAction : IEditorDropHandler
    {
        private readonly ConnectableStartObject startObject;
        private readonly OngekiObjectBase childViewModel;
        private readonly Action callback;

        public ConnectableObjectDropAction(ConnectableStartObject startObject, ConnectableChildObjectBase childObject, Action callback = default)
        {
            this.startObject = startObject;
            childViewModel = CacheLambdaActivator.CreateInstance(childObject.GetType()) as OngekiObjectBase;
            this.callback = callback;
        }

        public void Drop(FumenVisualEditorViewModel editor, Point dragEndPoint)
        {
            if (dragEndPoint.Y > editor.TotalDurationHeight || dragEndPoint.Y < 0)
            {
                editor.Toast.ShowMessage("无法添加物件到音频范围之外");
                return;
            }

            var dragTGrid = TGridCalculator.ConvertYToTGrid_DesignMode(dragEndPoint.Y, editor);
            var endObj = startObject.Children.OfType<ConnectableEndObject>().FirstOrDefault();
            var isAppend = Keyboard.IsKeyDown(Key.LeftAlt) && endObj is null;
            var isFirst = true;

            if (endObj is not null && !isAppend && endObj.TGrid < dragTGrid)
                return;

            editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("添加物件", () =>
            {
                if (isAppend)
                    startObject.AddChildObject(childViewModel as ConnectableChildObjectBase);
                else
                    startObject.InsertChildObject(dragTGrid, childViewModel as ConnectableChildObjectBase);
                editor.MoveObjectTo(childViewModel, dragEndPoint);
                editor.Redraw(RedrawTarget.OngekiObjects);
                callback?.Invoke();
                if (isFirst)
                {
                    editor.NotifyObjectClicked(childViewModel);
                    isFirst = false;
                }
            }, () =>
            {
                startObject.RemoveChildObject(childViewModel as ConnectableChildObjectBase);
                editor.Redraw(RedrawTarget.OngekiObjects);
                callback?.Invoke();
            }));
        }
    }
}
