using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Threading;
using Gemini.Modules.Toolbox;
using Gemini.Modules.Toolbox.Models;
using Gemini.Modules.Toolbox.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Modules.FumenBulletPalleteListViewer;
using OngekiFumenEditor.Modules.FumenMetaInfoBrowser;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Controls;
using OngekiFumenEditor.Modules.FumenVisualEditor.Controls.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Dialogs;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Modules.FumenVisualEditorSettings;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public partial class FumenVisualEditorViewModel : PersistedDocument
    {
        public IEnumerable<DisplayObjectViewModelBase> SelectObjects => EditorViewModels.OfType<DisplayObjectViewModelBase>().Where(x => x.IsSelected);

        public void CopySelectedObjects()
        {
            //复制所选物件
        }

        public void PasteCopiesObjects()
        {
            //粘贴已被复制物件
        }

        public void OnFocusableChanged(ActionExecutionContext e)
        {
            Log.LogInfo($"OnFocusableChanged {e.EventArgs}");
        }

        public void OnBPMListChanged()
        {
            Redraw(RedrawTarget.TGridUnitLines);
        }

        public void OnMouseWheel(ActionExecutionContext e)
        {
            var arg = e.EventArgs as MouseWheelEventArgs;
            var scrollDelta = (int)(arg.Delta * Setting.MouseWheelTimelineSpeed);
            var tGrid = Setting.CurrentDisplayTimePosition + new GridOffset(0, scrollDelta);

            if (tGrid < TGrid.ZeroDefault)
                tGrid = TGrid.ZeroDefault;

            //Setting.CurrentDisplayTimePosition = tGrid;
        }

        internal void OnSelectPropertyChanged(DisplayObjectViewModelBase obj, bool value)
        {
            if (value)
            {
                if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    foreach (var o in SelectObjects.Where(x => x != obj))
                        o.IsSelected = false;
                }
                if (IoC.Get<IFumenObjectPropertyBrowser>() is IFumenObjectPropertyBrowser propertyBrowser)
                    propertyBrowser.OngekiObject = SelectObjects.Count() == 1 ? SelectObjects.First().ReferenceOngekiObject : default;
            }
            else
            {
                if (IoC.Get<IFumenObjectPropertyBrowser>() is IFumenObjectPropertyBrowser propertyBrowser && propertyBrowser.OngekiObject == obj.ReferenceOngekiObject)
                    propertyBrowser.OngekiObject = default;
            }
        }

        #region Keyboard Actions

        public void KeyboardAction_DeleteSelectingObjects()
        {
            //删除已选择的物件
            var selectedObject = SelectObjects.ToArray();
            var propertyBrowser = IoC.Get<IFumenObjectPropertyBrowser>();

            foreach (var obj in selectedObject)
            {
                EditorViewModels.Remove(obj);
                Fumen.RemoveObject(obj.ReferenceOngekiObject);
                if (propertyBrowser != null && propertyBrowser.OngekiObject == obj.ReferenceOngekiObject)
                    propertyBrowser.OngekiObject = default;
            }
            Redraw(RedrawTarget.OngekiObjects);
            //Log.LogInfo($"deleted {selectedObject.Length} objects.");
        }

        public void KeyboardAction_SelectAllObjects()
        {
            EditorViewModels.OfType<DisplayObjectViewModelBase>().ForEach(x => x.IsSelected = true);
        }

        public void KeyboardAction_CancelSelectingObjects()
        {
            //取消选择
            SelectObjects.ForEach(x => x.IsSelected = false);
        }

        #endregion

        #region Drag Actions

        public void OnMouseLeave(ActionExecutionContext e)
        {
            Log.LogInfo("OnMouseLeave");
            if (!(isMouseDown && (e.View as FrameworkElement)?.Parent is IInputElement parent))
                return;
            isMouseDown = false;
            isDragging = false;
            var pos = (e.EventArgs as MouseEventArgs).GetPosition(parent);
            SelectObjects.ForEach(x => x.OnDragEnd(pos));
            //e.Handled = true;
        }

        public void OnMouseUp(ActionExecutionContext e)
        {
            Log.LogInfo("OnMouseUp");
            if (!(isMouseDown && (e.View as FrameworkElement)?.Parent is IInputElement parent))
                return;

            var pos = (e.EventArgs as MouseEventArgs).GetPosition(parent);
            if (isDragging)
                SelectObjects.ToArray().ForEach(x => x.OnDragEnd(pos));

            isMouseDown = false;
            isDragging = false;
            //e.Handled = true;
        }

        public void OnMouseMove(ActionExecutionContext e)
        {
            //Log.LogInfo("OnMouseMove");
            var view = e.View as FrameworkElement;
            if (!(isMouseDown && view is not null && view.Parent is IInputElement parent))
                return;
            //e.Handled = true;
            var r = isDragging;
            Action<DisplayObjectViewModelBase, Point> dragCall = (vm, pos) =>
            {
                if (r)
                    vm.OnDragMoving(pos);
                else
                    vm.OnDragStart(pos);
            };
            isDragging = true;

            var pos = (e.EventArgs as MouseEventArgs).GetPosition(parent);
            if (VisualTreeUtility.FindParent<Visual>(view) is FrameworkElement uiElement)
            {
                var bound = new Rect(0, 0, uiElement.ActualWidth, uiElement.ActualHeight);
                if (bound.Contains(pos))
                {
                    SelectObjects.ToArray().ForEach(x => dragCall(x, pos));
                }
            }
            else
            {
                SelectObjects.ToArray().ForEach(x => dragCall(x, pos));
            }
        }

        public void OnMouseDown(ActionExecutionContext e)
        {
            Log.LogInfo("OnMouseDown");
            if ((e.EventArgs as MouseEventArgs).LeftButton == MouseButtonState.Pressed)
            {
                isMouseDown = true;
                isDragging = false;
                //e.Handled = true;
            }
            (e.View as FrameworkElement)?.Focus();
        }

        public void Grid_DragEnter(ActionExecutionContext e)
        {
            var arg = e.EventArgs as DragEventArgs;
            if (!arg.Data.GetDataPresent(ToolboxDragDrop.DataFormat))
                arg.Effects = DragDropEffects.None;
        }

        public void Grid_Drop(ActionExecutionContext e)
        {
            var arg = e.EventArgs as DragEventArgs;
            if (!arg.Data.GetDataPresent(ToolboxDragDrop.DataFormat))
                return;

            var mousePosition = arg.GetPosition(e.View as FrameworkElement);
            var displayObject = default(DisplayObjectViewModelBase);

            switch (arg.Data.GetData(ToolboxDragDrop.DataFormat))
            {
                case ToolboxItem toolboxItem:
                    displayObject = Activator.CreateInstance(toolboxItem.ItemType) as DisplayObjectViewModelBase;
                    break;
                case OngekiObjectDropParam dropParam:
                    displayObject = dropParam.OngekiObjectViewModel.Value;
                    break;
            }
            /*
                        if (displayObject is IEditorDisplayableViewModel editorObjectViewModel)
                            editorObjectViewModel.OnObjectCreated(displayObject.ReferenceOngekiObject, this);
            */
            AddOngekiObject(displayObject);
            var ry = CanvasHeight - mousePosition.Y + MinVisibleCanvasY;
            mousePosition.Y = ry;
            displayObject.MoveCanvas(mousePosition);
            Redraw(RedrawTarget.OngekiObjects);
        }

        #endregion
    }
}
