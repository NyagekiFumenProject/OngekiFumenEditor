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
        private readonly DisplayObjectViewModelBase childViewModel;
        private readonly Action callback;

        public ConnectableObjectDropAction(ConnectableStartObject startObject, ConnectableChildObjectBase childObject, Action callback = default)
        {
            this.startObject = startObject;
            childViewModel = CacheLambdaActivator.CreateInstance(childObject.ModelViewType) as DisplayObjectViewModelBase;
            childViewModel.ReferenceOngekiObject = childObject;
            this.callback = callback;
        }

        public void Drop(FumenVisualEditorViewModel editor, Point dragEndPoint)
        {
            var dragTGrid = TGridCalculator.ConvertYToTGrid(dragEndPoint.Y, editor);
            var isAppend = Keyboard.IsKeyDown(Key.LeftAlt) && startObject.Children.OfType<ConnectableEndObject>().None();

            editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("添加物件", () =>
            {
                childViewModel.OnObjectCreated(childViewModel.ReferenceOngekiObject, editor);
                if (isAppend)
                    startObject.AddChildObject(childViewModel.ReferenceOngekiObject as ConnectableChildObjectBase);
                else
                    startObject.InsertChildObject(dragTGrid, childViewModel.ReferenceOngekiObject as ConnectableChildObjectBase);
                childViewModel.MoveCanvas(dragEndPoint);
                editor.Redraw(RedrawTarget.OngekiObjects);
                callback?.Invoke();
            }, () =>
            {
                startObject.RemoveChildObject(childViewModel.ReferenceOngekiObject as ConnectableChildObjectBase);
                editor.Redraw(RedrawTarget.OngekiObjects);
                callback?.Invoke();
            }));
        }
    }
}
