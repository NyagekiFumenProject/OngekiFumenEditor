using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Modules.Toolbox;
using Gemini.Modules.Toolbox.Models;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Utils;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public partial class FumenVisualEditorViewModel : PersistedDocument
    {
        #region Visibilities

        private bool isLocked = default;
        public bool IsLocked
        {
            get => isLocked;
            set
            {
                Set(ref isLocked, value);
                NotifyOfPropertyChange(() => EditorObjectVisibility);
                NotifyOfPropertyChange(() => EditorLockedVisibility);
            }
        }

        private bool isUserRequestHideEditorObject = default;
        public bool IsUserRequestHideEditorObject
        {
            get => isUserRequestHideEditorObject;
            set
            {
                Set(ref isUserRequestHideEditorObject, value);
                NotifyOfPropertyChange(() => EditorObjectVisibility);
            }
        }

        public Visibility EditorLockedVisibility =>
            IsLocked
            ? Visibility.Hidden : Visibility.Visible;

        public Visibility EditorObjectVisibility =>
            IsLocked || // 编辑器被锁住
            IsUserRequestHideEditorObject // 用户要求隐藏(比如按下Q)
            ? Visibility.Hidden : Visibility.Visible;

        #endregion

        #region Selection

        private Visibility selectionVisibility;
        public Visibility SelectionVisibility
        {
            get => selectionVisibility;
            set => Set(ref selectionVisibility, value);
        }

        private Point selectionStartPosition;
        public Point SelectionStartPosition
        {
            get => selectionStartPosition;
            set => Set(ref selectionStartPosition, value);
        }

        private Point selectionCurrentCursorPosition;
        public Point SelectionCurrentCursorPosition
        {
            get => selectionCurrentCursorPosition;
            set
            {
                Set(ref selectionCurrentCursorPosition, value);
                RecalculateSelectionRect();
            }
        }

        private Rect selectionRect;
        public Rect SelectionRect
        {
            get => selectionRect;
            set => Set(ref selectionRect, value);
        }

        public bool IsRangeSelecting => SelectionVisibility == Visibility.Visible;
        public bool IsPreventMutualExclusionSelecting { get; set; }

        #endregion

        public IEnumerable<DisplayObjectViewModelBase> SelectObjects => EditorViewModels.OfType<DisplayObjectViewModelBase>().Where(x => x.IsSelected);

        private Point? currentCursorPosition;
        public Point? CurrentCursorPosition
        {
            get => currentCursorPosition;
            set => Set(ref currentCursorPosition, value);
        }

        private HashSet<DisplayObjectViewModelBase> currentCopiedSources = new();
        public IEnumerable<DisplayObjectViewModelBase> CurrentCopiedSources => currentCopiedSources;

        #region Selection Actions

        public void MenuItemAction_SelectAll()
        {
            IsPreventMutualExclusionSelecting = true;

            EditorViewModels.OfType<DisplayObjectViewModelBase>().ForEach(x => x.IsSelected = true);

            IsPreventMutualExclusionSelecting = false;
        }

        public void MenuItemAction_ReverseSelect()
        {
            IsPreventMutualExclusionSelecting = true;

            var selected = SelectObjects.ToArray();
            EditorViewModels.OfType<DisplayObjectViewModelBase>().ForEach(x => x.IsSelected = !selected.Contains(x));

            IsPreventMutualExclusionSelecting = false;
        }

        public void MenuItemAction_CopySelectedObjects()
        {
            if (IsLocked)
                return;
            //复制所选物件
            currentCopiedSources.Clear();
            currentCopiedSources.AddRange(SelectObjects);
            Log.LogInfo($"钦定 {currentCopiedSources.Count} 个物件作为复制源");
        }

        public enum PasteMirrorOption
        {
            XGridZeroMirror,
            SelectedRangeCenterXGridMirror,
            SelectedRangeCenterTGridMirror,
            None
        }

        public void MenuItemAction_PasteCopiesObjects()
            => PasteCopiesObjects(PasteMirrorOption.None);
        public void MenuItemAction_PasteCopiesObjectsAsSelectedRangeCenterXGridMirror()
            => PasteCopiesObjects(PasteMirrorOption.SelectedRangeCenterXGridMirror);
        public void MenuItemAction_PasteCopiesObjectsAsSelectedRangeCenterTGridMirror()
            => PasteCopiesObjects(PasteMirrorOption.SelectedRangeCenterTGridMirror);
        public void MenuItemAction_PasteCopiesObjectsAsXGridZeroMirror()
            => PasteCopiesObjects(PasteMirrorOption.XGridZeroMirror);

        public void PasteCopiesObjects(PasteMirrorOption mirrorOption)
        {
            if (IsLocked)
                return;
            //粘贴已被复制物件
            TryCancelAllObjectSelecting();
            var count = 0;
            var newObjects = currentCopiedSources.Select(x => x.Copy()).FilterNull().ToArray();
            var mirrorTGrid = CalculateTGridMirror(newObjects, mirrorOption);
            var mirrorXGrid = CalculateXGridMirror(newObjects, mirrorOption);
            foreach (var displayObjectView in newObjects)
            {
                AddObject(displayObjectView);
                displayObjectView.IsSelected = true;
                count++;

                if (mirrorTGrid is not null && displayObjectView.ReferenceOngekiObject is ITimelineObject timelineObject)
                {
                    var tGrid = timelineObject.TGrid;
                    var offset = mirrorTGrid - tGrid;
                    var newTGrid = mirrorTGrid + offset;
                    timelineObject.TGrid = newTGrid;
                }

                if (mirrorXGrid is not null && displayObjectView.ReferenceOngekiObject is IHorizonPositionObject horizonPositionObject)
                {
                    var xGrid = horizonPositionObject.XGrid;
                    var offset = mirrorXGrid - xGrid;
                    var newXGrid = mirrorXGrid + offset;
                    horizonPositionObject.XGrid = newXGrid;
                }
            };
            Log.LogInfo($"已粘贴生成 {count} 个物件.");
            Redraw(RedrawTarget.OngekiObjects | RedrawTarget.TGridUnitLines);
        }

        private XGrid CalculateXGridMirror(DisplayObjectViewModelBase[] newObjects, PasteMirrorOption mirrorOption)
        {
            if (mirrorOption == PasteMirrorOption.XGridZeroMirror)
                return XGrid.Zero;

            if (mirrorOption == PasteMirrorOption.SelectedRangeCenterXGridMirror)
            {
                (var min, var max) = newObjects
                .Select(x => x.ReferenceOngekiObject as IHorizonPositionObject)
                .FilterNull()
                .MaxMinBy(x => x.XGrid, (a, b) =>
                {
                    if (a > b)
                        return 1;
                    if (a < b)
                        return -1;
                    return 0;
                });

                var diff = max - min;
                var mirror = min + new GridOffset(0, diff.TotalGrid(min.ResX) / 2);
                return mirror;
            }

            return default;
        }

        private TGrid CalculateTGridMirror(DisplayObjectViewModelBase[] newObjects, PasteMirrorOption mirrorOption)
        {
            if (mirrorOption != PasteMirrorOption.SelectedRangeCenterTGridMirror)
                return default;

            (var min, var max) = newObjects
                .Select(x => x.ReferenceOngekiObject as ITimelineObject)
                .FilterNull()
                .MaxMinBy(x => x.TGrid, (a, b) =>
                {
                    if (a > b)
                        return 1;
                    if (a < b)
                        return -1;
                    return 0;
                });

            var diff = max - min;
            var mirror = min + new GridOffset(0, diff.TotalGrid(min.ResT) / 2);
            return mirror;
        }

        private Dictionary<ITimelineObject, double> cacheObjectAudioTime = new();
        private DisplayObjectViewModelBase mouseDownHitObject;
        private Point? mouseDownHitObjectPosition;

        public void MenuItemAction_RememberSelectedObjectAudioTime()
        {
            cacheObjectAudioTime.Clear();
            foreach (var obj in SelectObjects)
            {
                if (obj.ReferenceOngekiObject is ITimelineObject timelineObject)
                    cacheObjectAudioTime[timelineObject] = TGridCalculator.ConvertTGridToY(timelineObject.TGrid, this);
                else
                    Log.LogInfo($"无法记忆此物件，因为此物件没有实现ITimelineObject : {obj.ReferenceOngekiObject}");
            }

            Log.LogInfo($"已记忆 {cacheObjectAudioTime.Count} 个物件的音频时间");
        }

        public void MenuItemAction_RecoverySelectedObjectToAudioTime()
        {
            var recoverTargets = EditorViewModels
                .OfType<DisplayObjectViewModelBase>()
                .Select(x => x.ReferenceOngekiObject as ITimelineObject)
                .Select(x => cacheObjectAudioTime.TryGetValue(x, out var audioTime) ? (x, audioTime) : default)
                .Where(x => x.x is not null)
                .OrderBy(x => x.audioTime)
                .ToList();

            var undoTargets = recoverTargets.Select(x => x.x).Select(x => (x, x.TGrid)).ToList();

            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("恢复物件到音频时间",
                () =>
                {
                    Log.LogInfo($"开始恢复物件时间...");
                    foreach ((var timelineObject, var audioTime) in recoverTargets)
                        timelineObject.TGrid = TGridCalculator.ConvertYToTGrid(audioTime, this);
                    Log.LogInfo($"已恢复 {recoverTargets.Count} 个物件到音频时间...");
                }, () =>
                {
                    foreach ((var timelineObject, var undoTGrid) in undoTargets)
                        timelineObject.TGrid = undoTGrid;
                    Log.LogInfo($"已撤回 {recoverTargets.Count} 个物件的音频时间恢复...");
                }
            ));
        }

        #endregion

        private void SelectRangeObjects(Rect selectionRect)
        {
            var selectObjects = EditorViewModels
                .OfType<DisplayObjectViewModelBase>()
                .Where(x => selectionRect.Contains(x.ReferenceOngekiObject is not IHorizonPositionObject ? selectionRect.X : x.CanvasX, x.CanvasY + Setting.JudgeLineOffsetY));

            foreach (var o in selectObjects)
            {
                o.IsSelected = true;
            }
        }

        public void OnFocusableChanged(ActionExecutionContext e)
        {
            Log.LogInfo($"OnFocusableChanged {e.EventArgs}");
        }

        public void OnTimeSignatureListChanged()
        {
            Redraw(RedrawTarget.TGridUnitLines);
        }

        #region Keyboard Actions

        public void KeyboardAction_DeleteSelectingObjects()
        {
            if (IsLocked)
                return;

            //删除已选择的物件
            var selectedObject = SelectObjects.ToArray();

            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("删除物件", () =>
            {
                foreach (var obj in selectedObject)
                    RemoveObject(obj);

                Redraw(RedrawTarget.OngekiObjects);
                Log.LogInfo($"deleted {selectedObject.Length} objects.");
            }, () =>
            {
                foreach (var obj in selectedObject)
                {
                    AddObject(obj);
                }

                Redraw(RedrawTarget.OngekiObjects);
                Log.LogInfo($"deleted {selectedObject.Length} objects.");
            }));
        }

        public void RemoveObject(DisplayObjectViewModelBase obj)
        {
            obj.IsSelected = false;
            EditorViewModels.Remove(obj);
            Fumen.RemoveObject(obj.ReferenceOngekiObject);
            CurrentDisplayEditorViewModels.Clear();

            var propertyBrowser = IoC.Get<IFumenObjectPropertyBrowser>();
            if (propertyBrowser != null && propertyBrowser.OngekiObject == obj.ReferenceOngekiObject)
                propertyBrowser.SetCurrentOngekiObject(default, this);
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

        public void KeyboardAction_PlayOrPause()
        {
            IoC.Get<IAudioPlayerToolViewer>().RequestPlayOrPause();
        }

        public void KeyboardAction_HideOrShow()
        {
            IsUserRequestHideEditorObject = !IsUserRequestHideEditorObject;
        }
        #endregion

        #region Drag Actions

        public void OnMouseLeave(ActionExecutionContext e)
        {
            IoC.Get<CommonStatusBar>().SubRightMainContentViewModel.Message = string.Empty;
            OnMouseUp(e);
            /*
            if (IsLocked)
                return;

            //Log.LogInfo("OnMouseLeave");
            if (!(IsMouseDown && (e.View as FrameworkElement)?.Parent is IInputElement parent))
                return;
            IsMouseDown = false;
            IsDragging = false;
            var pos = (e.EventArgs as MouseEventArgs).GetPosition(parent);
            SelectObjects.ForEach(x => x.OnDragEnd(pos));
            //e.Handled = true;*/
        }

        public void OnMouseUp(ActionExecutionContext e)
        {
            if (IsLocked)
                return;

            if (!(isMouseDown && (e.View as FrameworkElement)?.Parent is IInputElement parent))
                return;

            if (IsRangeSelecting && SelectionCurrentCursorPosition != SelectionStartPosition)
            {
                SelectRangeObjects(SelectionRect);
            }
            else
            {
                var pos = (e.EventArgs as MouseEventArgs).GetPosition(parent);
                if (isDragging)
                {
                    SelectObjects.ToArray().ForEach(x => x.OnDragEnd(pos));
                }
                else
                {
                    //Log.LogDebug($"mouseDownHitObject = {mouseDownHitObject?.ReferenceOngekiObject}");

                    if (mouseDownHitObject is null)
                    {
                        //for object brush
                        if (BrushMode)
                        {
                            TryApplyBrushObject(pos);
                        }
                    }
                    else
                    {
                        if (mouseDownHitObjectPosition is Point p)
                            mouseDownHitObject?.OnMouseClick(p);
                    }
                }
            }

            isMouseDown = false;
            isDragging = false;
            SelectionVisibility = Visibility.Collapsed;
        }

        private void TryApplyBrushObject(Point p)
        {
            if (!CurrentCopiedSources.IsOnlyOne(out var copySouceObj))
                return;

            var newObjViewModel = copySouceObj.Copy();
            if (newObjViewModel is null)
            {
                Log.LogInfo($"笔刷模式下不支持创建{copySouceObj.ReferenceOngekiObject.Name}");
                return;
            }

            p.Y = CanvasHeight - p.Y + MinVisibleCanvasY;
            var v = new Vector2((float)p.X, (float)p.Y);

            System.Action undo = () =>
            {
                RemoveObject(newObjViewModel);
                Redraw(RedrawTarget.OngekiObjects);
            };

            System.Action redo = () =>
            {
                newObjViewModel.OnObjectCreated(newObjViewModel.ReferenceOngekiObject, this);
                newObjViewModel.MoveCanvas(p);
                var dist = Vector2.Distance(v, new Vector2((float)newObjViewModel.CanvasX, (float)newObjViewModel.CanvasY));
                if (dist > 20)
                {
                    Log.LogDebug($"dist : {dist:F2} > 20 , undo&&discard");
                    undo();
                }
                else
                {
                    AddObject(newObjViewModel);
                    Redraw(RedrawTarget.OngekiObjects);
                }
            };

            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("刷子物件添加", redo, undo));
        }

        public void OnMouseDown(ActionExecutionContext e)
        {
            if (IsLocked)
                return;

            //Log.LogInfo("OnMouseDown");
            var view = e.View as FrameworkElement;

            if ((e.EventArgs as MouseEventArgs).LeftButton == MouseButtonState.Pressed)
            {
                isMouseDown = true;
                isDragging = false;
                var position = Mouse.GetPosition(view.Parent as IInputElement);
                var hitInputElement = (view.Parent as FrameworkElement)?.InputHitTest(position);
                var hitOngekiObjectViewModel = (hitInputElement as FrameworkElement)?.DataContext as DisplayObjectViewModelBase;
                mouseDownHitObject = null;
                mouseDownHitObjectPosition = default;

                if (hitOngekiObjectViewModel is null)
                {
                    TryCancelAllObjectSelecting();

                    //enable show selection
                    SelectionStartPosition = position;
                    SelectionCurrentCursorPosition = position;
                    SelectionVisibility = Visibility.Visible;
                }
                else
                {
                    SelectionVisibility = Visibility.Collapsed;
                    //Log.LogDebug($"hitOngekiObjectViewModel = {hitOngekiObjectViewModel.ReferenceOngekiObject}");
                    mouseDownHitObject = hitOngekiObjectViewModel;
                    mouseDownHitObjectPosition = position;
                }
            }
            (e.View as FrameworkElement)?.Focus();
        }

        public void OnMouseMove(ActionExecutionContext e)
        {
            //show current cursor position in statusbar
            UpdateCurrentCursorPosition(e);

            if (IsLocked)
                return;

            if (!(isMouseDown && (e.View as FrameworkElement)?.Parent is IInputElement parent))
                return;

            var r = isDragging;
            isDragging = true;
            var dragCall = new Action<DisplayObjectViewModelBase, Point>((vm, pos) =>
            {
                if (r)
                    vm.OnDragMoving(pos);
                else
                    vm.OnDragStart(pos);
            });

            var pos = (e.EventArgs as MouseEventArgs).GetPosition(parent);

            //检查判断，确定是拖动已选物品位置，还是说拉框选择区域
            if (IsRangeSelecting)
            {
                //拉框
                SelectionCurrentCursorPosition = pos;
            }
            else
            {
                //拖动已选物件
                SelectObjects.ForEach(x => dragCall(x, pos));
            }
        }

        #region Object Click&Selection

        public void TryCancelAllObjectSelecting(params DisplayObjectViewModelBase[] expects)
        {
            if (!(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || IsRangeSelecting || IsPreventMutualExclusionSelecting))
            {
                foreach (var o in SelectObjects.Where(x => !expects.Contains(x)))
                    o.IsSelected = false;
            }
        }

        public void NotifyObjectClicked(DisplayObjectViewModelBase obj)
        {
            if (SelectObjects.Take(2).Count() > 1) //比如你目前有多个已选择的，但你单点了一个
                obj.IsSelected = true;
            else
                obj.IsSelected = !obj.IsSelected;
            TryCancelAllObjectSelecting(obj);
        }

        #endregion

        private void RecalculateSelectionRect()
        {
            var x = Math.Min(SelectionStartPosition.X, SelectionCurrentCursorPosition.X);
            var y = MaxVisibleCanvasY - Math.Min(SelectionStartPosition.Y, SelectionCurrentCursorPosition.Y);

            var width = Math.Abs(SelectionStartPosition.X - SelectionCurrentCursorPosition.X);
            var height = Math.Abs(SelectionStartPosition.Y - SelectionCurrentCursorPosition.Y);

            y = y - height + Setting.JudgeLineOffsetY;

            SelectionRect = new Rect(x, y, width, height);
            //Log.LogDebug($"SelectionRect = {SelectionRect}");
        }

        private void UpdateCurrentCursorPosition(ActionExecutionContext e)
        {
            var view = e.View as FrameworkElement;
            var contentViewModel = IoC.Get<CommonStatusBar>().SubRightMainContentViewModel;
            if (view.Parent is not IInputElement parent)
            {
                contentViewModel.Message = string.Empty;
                CurrentCursorPosition = null;
                return;
            }

            var pos = (e.EventArgs as MouseEventArgs).GetPosition(parent);
            var canvasY = MaxVisibleCanvasY - pos.Y;
            var canvasX = pos.X;

            var tGrid = TGridCalculator.ConvertYToTGrid(canvasY, this);
            var xGrid = XGridCalculator.ConvertXToXGrid(canvasX, this);
            contentViewModel.Message = $"C[{canvasX:F2},{canvasY:F2}] {(tGrid is not null ? $"T[{tGrid.Unit},{tGrid.Grid}]" : "T[N/A]")} X[{xGrid.Unit:F2},{xGrid.Grid}]";
            CurrentCursorPosition = new(canvasX, canvasY);
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
            mousePosition.Y = CanvasHeight - mousePosition.Y + MinVisibleCanvasY;

            switch (arg.Data.GetData(ToolboxDragDrop.DataFormat))
            {
                case ToolboxItem toolboxItem:
                    new DefaultToolBoxDropAction(toolboxItem).Drop(this, mousePosition);
                    break;
                case IEditorDropHandler dropHandler:
                    dropHandler.Drop(this, mousePosition);
                    break;
            }
        }

        #endregion

        public void OnMouseWheel(ActionExecutionContext e)
        {
            var arg = e.EventArgs as MouseWheelEventArgs;
            arg.Handled = true;

            var delta = Math.Sign(arg.Delta) * AnimatedScrollViewer.VerticalScrollingDistance;
            var _totalVerticalOffset = Math.Min(Math.Max(0, AnimatedScrollViewer.VerticalOffset - delta), AnimatedScrollViewer.ScrollableHeight);
            AnimatedScrollViewer.ScrollToVerticalOffsetWithAnimation(_totalVerticalOffset);
        }

        #region Lock/Unlock User Interaction

        /// <summary>
        /// 锁住编辑器所有交互操作，用户无法对此编辑器做任何的操作
        /// </summary>
        public void LockAllUserInteraction()
        {
            if (IsLocked)
                return;
            IsLocked = true;
            SelectObjects.ToArray().ForEach(x => x.IsSelected = false);
            Log.LogInfo($"Editor is locked now.");
        }

        /// <summary>
        /// 接触对编辑器用户操作的封锁
        /// </summary>
        public void UnlockAllUserInteraction()
        {
            if (!IsLocked)
                return;
            IsLocked = false;
            Log.LogInfo($"Editor is unlocked now.");
        }

        #endregion
    }
}
