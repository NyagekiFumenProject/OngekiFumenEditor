using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
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
        private readonly OngekiObjectBase childObject;
        private readonly Action callback;

        public ConnectableObjectDropAction(ConnectableStartObject startObject, ConnectableChildObjectBase childObject, Action callback = default)
        {
            this.startObject = startObject;
            this.childObject = childObject/*CacheLambdaActivator.CreateInstance(childObject.GetType()) as OngekiObjectBase*/;
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

            editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("添加Next物件", () =>
            {
                if (isAppend)
                    startObject.AddChildObject(childObject as ConnectableChildObjectBase);
                else
                    startObject.InsertChildObject(dragTGrid, childObject as ConnectableChildObjectBase);
                editor.MoveObjectTo(childObject, dragEndPoint);
                if (isFirst)
                {
                    editor.NotifyObjectClicked(childObject);
                    isFirst = false;
                }
                callback?.Invoke();
            }, () =>
            {
                //startObject.RemoveChildObject(childViewModel as ConnectableChildObjectBase);
                editor.RemoveObject(childObject);
                callback?.Invoke();
            }));
        }
    }
}
