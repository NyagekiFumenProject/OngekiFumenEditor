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
        private bool isLocked = default;
        public bool IsLocked
        {
            get => isLocked;
            set
            {
                Set(ref isLocked, value);
                NotifyOfPropertyChange(() => IsLockedVisible);
            }
        }

        public Visibility IsLockedVisible => IsLocked ? Visibility.Hidden : Visibility.Visible;

        public IEnumerable<DisplayObjectViewModelBase> SelectObjects => EditorViewModels.OfType<DisplayObjectViewModelBase>().Where(x => x.IsSelected);

        public void CopySelectedObjects()
        {
            if (IsLocked)
                return;
            //复制所选物件
        }

        public void PasteCopiesObjects()
        {
            if (IsLocked)
                return;
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
            if (IsLocked)
                return;

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
            if (IsLocked)
                return;

            EditorViewModels.OfType<DisplayObjectViewModelBase>().ForEach(x => x.IsSelected = true);
        }

        public void KeyboardAction_CancelSelectingObjects()
        {
            if (IsLocked)
                return;

            //取消选择
            SelectObjects.ForEach(x => x.IsSelected = false);
        }

        #endregion

        #region Drag Actions

        public void OnMouseLeave(ActionExecutionContext e)
        {
            if (IsLocked)
                return;

            //Log.LogInfo("OnMouseLeave");
            if (!(IsMouseDown && (e.View as FrameworkElement)?.Parent is IInputElement parent))
                return;
            IsMouseDown = false;
            IsDragging = false;
            var pos = (e.EventArgs as MouseEventArgs).GetPosition(parent);
            SelectObjects.ForEach(x => x.OnDragEnd(pos));
            //e.Handled = true;
        }

        public void OnMouseUp(ActionExecutionContext e)
        {
            if (IsLocked)
                return;

            //Log.LogInfo("OnMouseUp");
            if (!(IsMouseDown && (e.View as FrameworkElement)?.Parent is IInputElement parent))
                return;

            var pos = (e.EventArgs as MouseEventArgs).GetPosition(parent);
            if (IsDragging)
                SelectObjects.ToArray().ForEach(x => x.OnDragEnd(pos));

            IsMouseDown = false;
            IsDragging = false;
            //e.Handled = true;
        }

        public void OnMouseMove(ActionExecutionContext e)
        {
            if (IsLocked)
                return;

            //Log.LogInfo("OnMouseMove");
            var view = e.View as FrameworkElement;
            if (!(IsMouseDown && view is not null && view.Parent is IInputElement parent))
                return;
            //e.Handled = true;
            var r = IsDragging;
            Action<DisplayObjectViewModelBase, Point> dragCall = (vm, pos) =>
            {
                if (r)
                    vm.OnDragMoving(pos);
                else
                    vm.OnDragStart(pos);
            };
            IsDragging = true;

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
            if (IsLocked)
                return;

            //Log.LogInfo("OnMouseDown");
            if ((e.EventArgs as MouseEventArgs).LeftButton == MouseButtonState.Pressed)
            {
                IsMouseDown = true;
                IsDragging = false;
                //e.Handled = true;
            }
            (e.View as FrameworkElement)?.Focus();
        }

        public void Grid_DragEnter(ActionExecutionContext e)
        {
            if (IsLocked)
            {
                Log.LogWarn($"discard user actions because editor was locked.");
                return;
            }

            var arg = e.EventArgs as DragEventArgs;
            if (!arg.Data.GetDataPresent(ToolboxDragDrop.DataFormat))
                arg.Effects = DragDropEffects.None;
        }

        public void Grid_Drop(ActionExecutionContext e)
        {
            if (IsLocked)
            {
                Log.LogWarn($"discard user actions because editor was locked.");
                return;
            }

            var arg = e.EventArgs as DragEventArgs;
            if (!arg.Data.GetDataPresent(ToolboxDragDrop.DataFormat))
                return;

            var mousePosition = arg.GetPosition(e.View as FrameworkElement);
            var displayObject = default(DisplayObjectViewModelBase);

            switch (arg.Data.GetData(ToolboxDragDrop.DataFormat))
            {
                case ToolboxItem toolboxItem:
                    displayObject = CacheLambdaActivator.CreateInstance(toolboxItem.ItemType) as DisplayObjectViewModelBase;
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

        #region Lock/Unlock User Interaction

        /// <summary>
        /// 锁住编辑器所有交互操作，用户无法对此编辑器做任何的操作
        /// </summary>
        public void LockAllUserInteraction()
        {
            IsLocked = true;
            SelectObjects.ToArray().ForEach(x => x.IsSelected = false);
            Log.LogInfo($"Editor is locked now.");
        }

        /// <summary>
        /// 接触对编辑器用户操作的封锁
        /// </summary>
        public void UnlockAllUserInteraction()
        {
            IsLocked = false;
            Log.LogInfo($"Editor is unlocked now.");
        }

        #endregion
    }
}
