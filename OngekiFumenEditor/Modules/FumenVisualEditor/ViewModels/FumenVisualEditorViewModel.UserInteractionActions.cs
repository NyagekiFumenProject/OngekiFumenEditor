using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Modules.Toolbox;
using Gemini.Modules.Toolbox.Models;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views.UI;
using OngekiFumenEditor.Utils;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public Toast Toast => (GetView() as FumenVisualEditorView)?.mainToast;

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

            if (currentCopiedSources.Count == 0)
                ToastNotify($"清空复制列表");
            else
                ToastNotify($"钦定 {currentCopiedSources.Count} 个物件作为复制源 {(currentCopiedSources.Count == 1 ? ",并作为刷子模式的批量生成源" : string.Empty)}");
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
            //先取消选择所有的物件
            TryCancelAllObjectSelecting();
            var newObjects = currentCopiedSources.Select(x => x.Copy()).FilterNull().ToList();
            var mirrorTGrid = CalculateTGridMirror(newObjects, mirrorOption);
            var mirrorXGrid = CalculateXGridMirror(newObjects, mirrorOption);

            var redo = new System.Action(() => { });
            var undo = new System.Action(() => { });

            var idMap = new Dictionary<int, int>();

            var partOfConnectableObjects = newObjects
                .Select(x => x.ReferenceOngekiObject)
                .OfType<ConnectableObjectBase>()
                .GroupBy(x => x.RecordId);

            foreach (var lane in partOfConnectableObjects.Where(x => !x.OfType<ConnectableStartObject>().Any()).ToArray())
            {
                var headChildObject = lane.First() as ConnectableChildObjectBase;
                var refRecordId = -headChildObject.RecordId;
                var refSourceHeadChildObject = currentCopiedSources.Select(x => x.ReferenceOngekiObject).OfType<ConnectableChildObjectBase>().FirstOrDefault(x => x.RecordId == refRecordId);

                var newStartObjectViewModel = LambdaActivator.CreateInstance(refSourceHeadChildObject.ReferenceStartObject.ModelViewType) as DisplayObjectViewModelBase;
                var newStartObject = newStartObjectViewModel.ReferenceOngekiObject;

                newStartObject.Copy(headChildObject, Fumen);

                newObjects.RemoveAll(x => x.ReferenceOngekiObject == headChildObject);
                newObjects.Insert(0,newStartObjectViewModel);

                Log.LogDebug($"detect non-include start object copying , remove head of children and add new start object, headChildObject : {headChildObject}");
            }

            foreach (var displayObjectView in newObjects)
            {
                if (displayObjectView.ReferenceOngekiObject is ITimelineObject timelineObject)
                {
                    var tGrid = timelineObject.TGrid.CopyNew();
                    undo += () => timelineObject.TGrid = tGrid.CopyNew();

                    if (mirrorTGrid is not null)
                    {
                        var offset = mirrorTGrid - tGrid;
                        var newTGrid = mirrorTGrid + offset;

                        redo += () => timelineObject.TGrid = newTGrid.CopyNew();
                    }
                    else
                        redo += () => timelineObject.TGrid = tGrid.CopyNew();
                }

                if (displayObjectView.ReferenceOngekiObject is IHorizonPositionObject horizonPositionObject)
                {
                    var xGrid = horizonPositionObject.XGrid.CopyNew();
                    undo += () => horizonPositionObject.XGrid = xGrid.CopyNew();

                    if (mirrorXGrid is not null)
                    {
                        var offset = mirrorXGrid - xGrid;
                        var newXGrid = mirrorXGrid + offset;

                        redo += () => horizonPositionObject.XGrid = newXGrid.CopyNew();
                    }
                    else
                        redo += () => horizonPositionObject.XGrid = xGrid.CopyNew();
                }

                var isSelect = displayObjectView.IsSelected;

                switch (displayObjectView.ReferenceOngekiObject)
                {
                    case ConnectableStartObject startObject:
                        var rawId = startObject.RecordId;
                        redo += () =>
                        {
                            AddObject(displayObjectView);
                            var newId = startObject.RecordId;
                            idMap[rawId] = newId;
                            displayObjectView.IsSelected = true;
                        };

                        undo += () =>
                        {
                            RemoveObject(displayObjectView);
                            startObject.RecordId = rawId;
                            displayObjectView.IsSelected = isSelect;
                        };
                        break;
                    case ConnectableChildObjectBase childObject:
                        var rawChildId = childObject.RecordId;
                        redo += () =>
                        {
                            if (idMap.TryGetValue(rawChildId, out var newChildId))
                                childObject.RecordId = newChildId;
                            AddObject(displayObjectView);
                            displayObjectView.IsSelected = true;
                        };

                        undo += () =>
                        {
                            RemoveObject(displayObjectView);
                            childObject.RecordId = rawChildId;
                            displayObjectView.IsSelected = isSelect;
                        };
                        break;
                    default:
                        redo += () =>
                        {
                            AddObject(displayObjectView);
                            displayObjectView.IsSelected = true;
                        };

                        undo += () =>
                        {
                            RemoveObject(displayObjectView);
                            displayObjectView.IsSelected = isSelect;
                        };
                        break;
                }
            };

            redo += () =>
            {
                Redraw(RedrawTarget.OngekiObjects | RedrawTarget.TGridUnitLines);
                ToastNotify($"已粘贴生成 {newObjects.Count} 个物件");
            };

            undo += () =>
            {
                Redraw(RedrawTarget.OngekiObjects | RedrawTarget.TGridUnitLines);
                ToastNotify($"已撤销粘贴生成");
            };

            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("复制粘贴", redo, undo));
        }

        private XGrid CalculateXGridMirror(IEnumerable<DisplayObjectViewModelBase> newObjects, PasteMirrorOption mirrorOption)
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

        private TGrid CalculateTGridMirror(IEnumerable<DisplayObjectViewModelBase> newObjects, PasteMirrorOption mirrorOption)
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
        private Point mouseStartPosition;
        /// <summary>
        /// 表示指针是否出拖动出滚动范围
        /// </summary>
        private bool dragOutBound;
        private int currentDraggingActionId;

        public void MenuItemAction_RememberSelectedObjectAudioTime()
        {
            cacheObjectAudioTime.Clear();
            foreach (var obj in SelectObjects)
            {
                if (obj.ReferenceOngekiObject is ITimelineObject timelineObject)
                    cacheObjectAudioTime[timelineObject] = TGridCalculator.ConvertTGridToY(timelineObject.TGrid, this);
                else
                    ToastNotify($"无法记忆此物件，因为此物件没有实现ITimelineObject : {obj.ReferenceOngekiObject}");
            }

            ToastNotify($"已记忆 {cacheObjectAudioTime.Count} 个物件的音频时间");
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

                    ToastNotify($"已恢复 {recoverTargets.Count} 个物件到音频时间...");
                }, () =>
                {
                    foreach ((var timelineObject, var undoTGrid) in undoTargets)
                        timelineObject.TGrid = undoTGrid;
                    ToastNotify($"已撤回恢复 {recoverTargets.Count} 个物件到音频时间...");
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
            }, () =>
            {
                foreach (var obj in selectedObject)
                {
                    AddObject(obj);
                }

                Redraw(RedrawTarget.OngekiObjects);
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
                    var cp = pos;
                    cp.Y = CanvasHeight - cp.Y + MinVisibleCanvasY;
                    SelectObjects.ToArray().ForEach(x => x.OnDragEnd(cp));
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
            currentDraggingActionId = int.MaxValue;
        }

        private void TryApplyBrushObject(Point p)
        {
            if (!CurrentCopiedSources.IsOnlyOne(out var copySouceObj))
                return;

            var newObjViewModel = copySouceObj.Copy();
            if (newObjViewModel is null
                //不支持笔刷模式下新建以下玩意
                || newObjViewModel.ReferenceOngekiObject is ConnectableStartObject
                || newObjViewModel.ReferenceOngekiObject is ConnectableEndObject)
            {
                ToastNotify($"笔刷模式下不支持{copySouceObj?.ReferenceOngekiObject?.Name}");
                return;
            }

            p.Y = CanvasHeight - p.Y + MinVisibleCanvasY;
            var v = new Vector2((float)p.X, (float)p.Y);

            System.Action undo = () =>
            {
                if (newObjViewModel.ReferenceOngekiObject is ConnectableChildObjectBase childObject)
                {
                    (copySouceObj.ReferenceOngekiObject as ConnectableChildObjectBase)?.ReferenceStartObject.RemoveChildObject(childObject);
                }
                else
                {
                    RemoveObject(newObjViewModel);
                }
                Redraw(RedrawTarget.OngekiObjects);
            };

            System.Action redo = async () =>
            {
                newObjViewModel.OnObjectCreated(newObjViewModel.ReferenceOngekiObject, this);
                newObjViewModel.MoveCanvas(p);
                var dist = Vector2.Distance(v, new Vector2((float)newObjViewModel.CanvasX, (float)newObjViewModel.CanvasY));
                if (dist > 20)
                {
                    Log.LogDebug($"dist : {dist:F2} > 20 , undo&&discard");
                    undo();

                    Mouse.OverrideCursor = Cursors.No;
                    await Task.Delay(100);
                    Mouse.OverrideCursor = Cursors.Arrow;
                }
                else
                {
                    if (newObjViewModel.ReferenceOngekiObject is ConnectableChildObjectBase childObject)
                    {
                        (copySouceObj.ReferenceOngekiObject as ConnectableChildObjectBase)?.ReferenceStartObject.AddChildObject(childObject);
                    }
                    else
                    {
                        AddObject(newObjViewModel);
                    }
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
                mouseStartPosition = position;
                dragOutBound = false;

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
            if ((e.View as FrameworkElement)?.Parent is not IInputElement parent)
                return;
            currentDraggingActionId = int.MaxValue;
            OnMouseMove((e.EventArgs as MouseEventArgs).GetPosition(parent));
        }
        public async void OnMouseMove(Point pos)
        {
            //show current cursor position in statusbar
            UpdateCurrentCursorPosition(pos);

            if (IsLocked)
                return;

            if (!isMouseDown)
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

            var rp = 1 - pos.Y / CanvasHeight;
            var srp = 1 - mouseStartPosition.Y / CanvasHeight;
            var offsetY = 0d;

            //const double dragDist = 0.7;
            const double trigPrecent = 0.15;
            const double autoScrollSpeed = 7;

            var offsetYAcc = 0d;
            if (rp >= (1 - trigPrecent) && dragOutBound)
                offsetYAcc = (rp - (1 - trigPrecent)) / trigPrecent;
            else if (rp <= trigPrecent && dragOutBound)
                offsetYAcc = rp / trigPrecent - 1;
            else if (rp < 1 - trigPrecent && rp > trigPrecent)
                dragOutBound = true; //当指针在滑动范围外面，那么就可以进行任何的滑动操作了，避免指针从滑动范围内开始就滚动
            offsetY = offsetYAcc * autoScrollSpeed;

            var prev = AnimatedScrollViewer.CurrentVerticalOffset;
            var y = MinVisibleCanvasY + Setting.JudgeLineOffsetY + offsetY;

            //Log.LogDebug($"pos={pos.X:F2},{pos.Y:F2} offsetYAcc={offsetYAcc:F2} dragOutBound={dragOutBound} y={y:F2}");

            if (offsetY != 0)
                ScrollTo(y);

            //检查判断，确定是拖动已选物品位置，还是说拉框选择区域
            if (IsRangeSelecting)
            {
                //拉框
                var p = pos;
                p.Y += offsetY;
                p.Y -= 2 * offsetY;
                SelectionCurrentCursorPosition = p;

                var p2 = SelectionStartPosition;
                p2.Y += prev - AnimatedScrollViewer.CurrentVerticalOffset;
                SelectionStartPosition = p2;
                //Log.LogDebug($"prevY:{-AnimatedScrollViewer.CurrentVerticalOffset + prev:F2} offsetY:{offsetY:F2}");
            }
            else
            {
                //拖动已选物件
                var cp = pos;
                cp.Y = CanvasHeight - cp.Y + MinVisibleCanvasY;
                SelectObjects.ForEach(x => dragCall(x, cp));
            }

            //持续性的
            if (offsetY != 0)
            {
                var currentid = currentDraggingActionId = MathUtils.Random(int.MaxValue - 1);
                await Task.Delay(1000 / 60);
                if (currentDraggingActionId == currentid)
                    OnMouseMove(pos);
            }
        }

        #region Object Click&Selection

        public void TryCancelAllObjectSelecting(params DisplayObjectViewModelBase[] expects)
        {
            var objBrowser = IoC.Get<IFumenObjectPropertyBrowser>();
            var curBrowserObj = objBrowser.OngekiObject;

            if (!(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || IsRangeSelecting || IsPreventMutualExclusionSelecting))
            {
                foreach (var o in SelectObjects.Where(x => !expects.Contains(x)))
                {
                    o.IsSelected = false;
                    if (o.ReferenceOngekiObject == curBrowserObj)
                        objBrowser.SetCurrentOngekiObject(null, this);
                }
            }
        }

        public void NotifyObjectClicked(DisplayObjectViewModelBase obj)
        {
            var objBrowser = IoC.Get<IFumenObjectPropertyBrowser>();
            var curBrowserObj = objBrowser.OngekiObject;

            if (SelectObjects.Take(2).Count() > 1) //比如你目前有多个已选择的，但你单点了一个
            {
                TryCancelAllObjectSelecting(obj);
                obj.IsSelected = true;
                objBrowser.SetCurrentOngekiObject(obj.ReferenceOngekiObject, this);
            }
            else
            {
                obj.IsSelected = !obj.IsSelected;
                if (obj.IsSelected)
                    objBrowser.SetCurrentOngekiObject(obj.ReferenceOngekiObject, this);
                else if (obj.ReferenceOngekiObject == curBrowserObj)
                    objBrowser.SetCurrentOngekiObject(null, this);
                TryCancelAllObjectSelecting(obj);
            }
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
            var contentViewModel = IoC.Get<CommonStatusBar>().SubRightMainContentViewModel;
            if ((e.View as FrameworkElement)?.Parent is not IInputElement parent)
            {
                contentViewModel.Message = string.Empty;
                CurrentCursorPosition = null;
                return;
            }
        }

        private void UpdateCurrentCursorPosition(Point pos)
        {
            var contentViewModel = IoC.Get<CommonStatusBar>().SubRightMainContentViewModel;

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
            if (IsLocked)
                return;

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
            ToastNotify($"编辑器已锁住");
        }

        /// <summary>
        /// 接触对编辑器用户操作的封锁
        /// </summary>
        public void UnlockAllUserInteraction()
        {
            if (!IsLocked)
                return;
            IsLocked = false;
            ToastNotify($"编辑器已解锁");
        }

        #endregion

        private void ToastNotify(string message)
        {
            Toast.ShowMessage(message);
            Log.LogInfo(message);
        }
    }
}
