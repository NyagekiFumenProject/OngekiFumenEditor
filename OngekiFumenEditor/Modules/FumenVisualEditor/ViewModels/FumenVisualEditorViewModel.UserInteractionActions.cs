using Caliburn.Micro;
using DereTore.Common;
using Gemini.Framework;
using Gemini.Framework.Services;
using Gemini.Modules.Toolbox;
using Gemini.Modules.Toolbox.Models;
using Gemini.Modules.UndoRedo;
using Microsoft.CodeAnalysis.Differencing;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels.DropActions;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views.UI;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
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
                NotifyOfPropertyChange(() => IsDesignMode);
                NotifyOfPropertyChange(() => IsPreviewMode);
            }
        }

        private bool isUserRequestHideEditorObject = default;
        private bool IsUserRequestHideEditorObject
        {
            get => isUserRequestHideEditorObject;
            set
            {
                Set(ref isUserRequestHideEditorObject, value);
                NotifyOfPropertyChange(() => EditorObjectVisibility);
                NotifyOfPropertyChange(() => IsDesignMode);
                NotifyOfPropertyChange(() => IsPreviewMode);
            }
        }

        public Visibility EditorLockedVisibility =>
            IsLocked
            ? Visibility.Hidden : Visibility.Visible;

        public Visibility EditorObjectVisibility =>
            IsLocked || // 编辑器被锁住
            IsUserRequestHideEditorObject // 用户要求隐藏(比如按下Q)
            ? Visibility.Hidden : Visibility.Visible;

        public bool IsDesignMode => EditorObjectVisibility == Visibility.Visible;
        public bool IsPreviewMode => !IsDesignMode;

        #endregion

        #region Selection

        private Visibility selectionVisibility;
        public Visibility SelectionVisibility
        {
            get => selectionVisibility;
            set => Set(ref selectionVisibility, value);
        }

        private Vector2 selectionStartPosition;
        public Vector2 SelectionStartPosition
        {
            get => selectionStartPosition;
            set => Set(ref selectionStartPosition, value);
        }

        private Vector2 selectionCurrentCursorPosition;
        public Vector2 SelectionCurrentCursorPosition
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

        public IEnumerable<ISelectableObject> SelectObjects => Fumen.GetAllDisplayableObjects().OfType<ISelectableObject>().Where(x => x.IsSelected).Distinct();

        private Point? currentCursorPosition;
        public Point? CurrentCursorPosition
        {
            get => currentCursorPosition;
            set => Set(ref currentCursorPosition, value);
        }
        private Dictionary<OngekiObjectBase, Rect> hits = new();
        private OngekiObjectBase mouseDownNextHitObject;
        private Point mouseCanvasStartPosition;
        private double startXOffset;
        private double startScrollOffset;
        private bool isCanvasDragging;
        private bool isMiddleMouseDown;
        private MouseButtonState prevRightButtonState;
        private Point contextMenuPosition;

        public Toast Toast => (GetView() as FumenVisualEditorView)?.mainToast;

        public ObjectInteractiveManager InteractiveManager { get; private set; } = new();

        #region provide extra MenuItem by plugins

        public void InitExtraMenuItems()
        {
            var ctxMenu = (GetView() as FumenVisualEditorView).EditorContextMenu;

            var extMenuItems = IoC.Get<IEditorExtraContextMenuBuilder>().BuildMenuItems(IoC.GetAll<IFumenVisualEditorExtraMenuItemHandler>(), this);
            foreach (var extMenuItem in extMenuItems)
                ctxMenu.Items.Add(extMenuItem);
        }

        #endregion

        #region Selection Actions

        public void MenuItemAction_SelectAll()
        {
            IsPreventMutualExclusionSelecting = true;

            Fumen.GetAllDisplayableObjects().OfType<ISelectableObject>().ForEach(x => x.IsSelected = true);
            IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(Editor);

            IsPreventMutualExclusionSelecting = false;
        }

        public void MenuItemAction_ReverseSelect()
        {
            IsPreventMutualExclusionSelecting = true;

            Fumen.GetAllDisplayableObjects().OfType<ISelectableObject>().ForEach(x => x.IsSelected = !x.IsSelected);
            IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(Editor);

            IsPreventMutualExclusionSelecting = false;
        }

        public async void MenuItemAction_CopySelectedObjects()
        {
            await IoC.Get<IFumenEditorClipboard>().CopyObjects(this, SelectObjects);
        }

        public enum PasteOption
        {
            XGridZeroMirror,
            SelectedRangeCenterXGridMirror,
            SelectedRangeCenterTGridMirror,
            Direct,
            None
        }

        public void MenuItemAction_PasteCopiesObjects()
        {
            var placePos = Mouse.GetPosition(GetView() as FrameworkElement);
            placePos.Y = ViewHeight - placePos.Y + Rect.MinY;
            PasteCopiesObjects(PasteOption.None, placePos);
        }

        public void MenuItemAction_PasteCopiesObjectsDirectly(ActionExecutionContext ctx)
        {
            PasteCopiesObjects(PasteOption.Direct, default(Point));
        }

        public void MenuItemAction_PasteCopiesObjects(ActionExecutionContext ctx)
            => PasteCopiesObjects(PasteOption.None, ctx);
        public void MenuItemAction_PasteCopiesObjectsAsSelectedRangeCenterXGridMirror(ActionExecutionContext ctx)
            => PasteCopiesObjects(PasteOption.SelectedRangeCenterXGridMirror, ctx);
        public void MenuItemAction_PasteCopiesObjectsAsSelectedRangeCenterTGridMirror(ActionExecutionContext ctx)
            => PasteCopiesObjects(PasteOption.SelectedRangeCenterTGridMirror, ctx);
        public void MenuItemAction_PasteCopiesObjectsAsXGridZeroMirror(ActionExecutionContext ctx)
            => PasteCopiesObjects(PasteOption.XGridZeroMirror, ctx);

        public void PasteCopiesObjects(PasteOption mirrorOption, ActionExecutionContext ctx)
        {
            var placePos = contextMenuPosition;
            placePos.Y = ViewHeight - placePos.Y + Rect.MinY;
            PasteCopiesObjects(mirrorOption, placePos);
        }

        public async void PasteCopiesObjects(PasteOption mirrorOption, Point? placePoint = default)
        {
            if (IsLocked)
                return;

            await IoC.Get<IFumenEditorClipboard>().PasteObjects(this, mirrorOption, placePoint);
        }

        private Dictionary<ITimelineObject, double> cacheObjectAudioTime = new();
        private OngekiObjectBase mouseDownHitObject;
        private Point? mouseDownHitObjectPosition;
        private Point mouseSelectRangeStartPosition;
        /// <summary>
        /// 表示指针是否出拖动出滚动范围
        /// </summary>
        private bool dragOutBound;
        private int currentDraggingActionId;

        public void MenuItemAction_RememberSelectedObjectAudioTime()
        {
            if (!IsDesignMode)
            {
                ToastNotify(Resources.EditorMustBeDesignMode);
                return;
            }

            cacheObjectAudioTime.Clear();
            foreach (var obj in SelectObjects)
            {
                if (obj is ITimelineObject timelineObject)
                    cacheObjectAudioTime[timelineObject] = TGridCalculator.ConvertTGridToY_DesignMode(timelineObject.TGrid, this);
                else
                    ToastNotify(Resources.CantRememberObjectByNotSupport.Format(obj));
            }

            ToastNotify(Resources.RememberObjects.Format(cacheObjectAudioTime.Count));
        }

        public void MenuItemAction_RecoverySelectedObjectToAudioTime()
        {
            if (!IsDesignMode)
            {
                ToastNotify(Resources.EditorMustBeDesignMode);
                return;
            }

            var recoverTargets = Fumen.GetAllDisplayableObjects()
                .OfType<ITimelineObject>()
                .Select(x => cacheObjectAudioTime.TryGetValue(x, out var audioTime) ? (x, audioTime) : default)
                .Where(x => x.x is not null)
                .OrderBy(x => x.audioTime)
                .ToList();

            var undoTargets = recoverTargets.Select(x => x.x).Select(x => (x, x.TGrid)).ToList();

            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.RecoveryObjectToAudioTime,
                () =>
                {
                    Log.LogInfo($"开始恢复物件时间...");
                    foreach ((var timelineObject, var audioTime) in recoverTargets)
                        timelineObject.TGrid = TGridCalculator.ConvertYToTGrid_DesignMode(audioTime, this);

                    ToastNotify(Resources.RecoveryObjectsSuccess.Format(recoverTargets.Count));
                }, () =>
                {
                    foreach ((var timelineObject, var undoTGrid) in undoTargets)
                        timelineObject.TGrid = undoTGrid.CopyNew();
                    ToastNotify(Resources.UndoRecoveryObjectsSuccess.Format(recoverTargets.Count));
                }
            ));
        }

        #endregion

        private void SelectRangeObjects()
        {
            if (!IsDesignMode)
            {
                ToastNotify(Resources.EditorMustBeDesignMode);
                return;
            }

            var topY = Math.Max(SelectionCurrentCursorPosition.Y, SelectionStartPosition.Y);
            var buttomY = Math.Min(SelectionCurrentCursorPosition.Y, SelectionStartPosition.Y);
            var rightX = Math.Max(SelectionCurrentCursorPosition.X, SelectionStartPosition.X);
            var leftX = Math.Min(SelectionCurrentCursorPosition.X, SelectionStartPosition.X);

            var minTGrid = TGridCalculator.ConvertYToTGrid_DesignMode(buttomY, this);
            var maxTGrid = TGridCalculator.ConvertYToTGrid_DesignMode(topY, this);
            var minXGrid = XGridCalculator.ConvertXToXGrid(leftX, this);
            var maxXGrid = XGridCalculator.ConvertXToXGrid(rightX, this);

            bool check(OngekiObjectBase obj)
            {
                if (obj is ITimelineObject timelineObject)
                {
                    if (timelineObject.TGrid > maxTGrid || timelineObject.TGrid < minTGrid)
                        return false;
                }

                if (obj is IHorizonPositionObject horizonPositionObject)
                {
                    if (horizonPositionObject.XGrid > maxXGrid || horizonPositionObject.XGrid < minXGrid)
                        return false;
                }

                return true;
            }

            var selectObjects = Fumen.GetAllDisplayableObjects()
                .OfType<OngekiObjectBase>()
                .Distinct()
                .Where(check)
                .ToArray();

            if (selectObjects.Length == 1)
                NotifyObjectClicked(selectObjects.FirstOrDefault());
            else
            {
                foreach (var o in selectObjects.OfType<ISelectableObject>())
                    o.IsSelected = true;
                IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(this);
            }
        }

        public void OnFocusableChanged(ActionExecutionContext e)
        {
            Log.LogInfo($"OnFocusableChanged {e.EventArgs}");
        }

        public void OnTimeSignatureListChanged()
        {
            //nothing but leave it empty.
        }

        public void ExecuteActionWithRememberCurrentTime(IUndoableAction action)
        {
            var curTime = CurrentPlayTime;
            var rememberAction = LambdaUndoAction.Create(action.Name, () =>
            {
                curTime = CurrentPlayTime;
                action.Execute();
            },
            () =>
            {
                action.Undo();
                ScrollTo(curTime);
            });

            UndoRedoManager.ExecuteAction(rememberAction);
        }

        #region Keyboard Actions

        public void KeyboardAction_FastPlaceDockableObjectToCenter()
            => KeyboardAction_FastPlaceDockableObject(LaneType.Center);
        public void KeyboardAction_FastPlaceDockableObjectToLeft()
            => KeyboardAction_FastPlaceDockableObject(LaneType.Left);
        public void KeyboardAction_FastPlaceDockableObjectToRight()
            => KeyboardAction_FastPlaceDockableObject(LaneType.Right);
        public void KeyboardAction_FastPlaceDockableObjectToWallLeft()
            => KeyboardAction_FastPlaceDockableObject(LaneType.WallLeft);
        public void KeyboardAction_FastPlaceDockableObjectToWallRight()
            => KeyboardAction_FastPlaceDockableObject(LaneType.WallRight);

        public void KeyboardAction_FastPlaceDockableObject(LaneType targetType)
        {
            if ((!SelectObjects.AtCount(1)) || SelectObjects.FirstOrDefault() is not ILaneDockable dockable)
            {
                ToastNotify(Resources.MustSelectOneTapOrHold);
                return;
            }

            KeyboardAction_FastPlaceDockableObject(targetType, dockable);
        }

        public void KeyboardAction_FastPlaceDockableObject(LaneType targetType, ILaneDockable dockable)
        {
            var dockableLanes = Fumen.Lanes
                .GetVisibleStartObjects(dockable.TGrid, dockable.TGrid)
                .Where(x => x.LaneType == targetType);

            var pickLane = dockableLanes.FirstOrDefault();

            var beforeXGrid = dockable.XGrid;
            var beforeHoldEndXGrid = (dockable as Hold)?.HoldEnd?.XGrid;
            var beforeLane = dockable.ReferenceLaneStart;

            //如果本身已经有轨道引用且是同一个类型的轨道，那么就判断一下位置,钦定下一条同类型轨道
            if (beforeLane is not null)
            {
                var curXGrid = dockable.XGrid;
                //获取轨道并计算对应的位置，然后排序，如果位置相同那么就再按RecordId去排序
                var pickableLanes = dockableLanes
                    .Select(x =>
                        new
                        {
                            Lane = x,
                            XGrid = x.CalulateXGrid(dockable.TGrid)
                        })
                    .FilterNullBy(x => x.XGrid)
                    .OrderBy((a, b) => a.XGrid.CompareTo(b.XGrid), (a, b) => a.Lane.RecordId.CompareTo(b.Lane.RecordId));

                var r = pickableLanes
                    .Where(x => x.XGrid >= curXGrid)
                    .Select(x => x.Lane);

                //这里考虑如果是切换到其他颜色轨道，那么可以直接原地变色。
                var pick = beforeLane.LaneType == targetType ? r.FindNextOrDefault(beforeLane) : r.FirstOrDefault();

                //如果pick为空，说明右侧再也没有合适的轨道可以放了，那么就尝试直接获取最左侧的轨道，重新开始
                if (pick is null)
                    pick = pickableLanes
                        .Select(x => x.Lane)
                        .FirstOrDefault();

                if (pick is not null)
                    pickLane = pick;
            }

            if (pickLane is null)
            {
                ToastNotify(Resources.CantPlaceObjectsByNoSuiteLanes.Format(targetType));
                return;
            }

            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.FastPlaceObjectToLane.Format(dockable.GetType().Name, targetType), () =>
            {
                dockable.ReferenceLaneStart = pickLane;
                if (dockable.ReferenceLaneStart is not null)
                {
                    if (dockable.ReferenceLaneStart.CalulateXGrid(dockable.TGrid) is XGrid xGrid)
                        dockable.XGrid = xGrid;

                    //如果是Hold还得他整理一下尾巴呢
                    if (dockable is Hold hold)
                    {
                        if (hold.HoldEnd is HoldEnd holdEnd && dockable.ReferenceLaneStart.CalulateXGrid(holdEnd.TGrid) is XGrid holdXGrid)
                            holdEnd.XGrid = holdXGrid;
                    }
                }
            }, () =>
            {
                dockable.ReferenceLaneStart = beforeLane;
                dockable.XGrid = beforeXGrid;

                if (dockable is Hold hold && hold.HoldEnd is HoldEnd holdEnd && beforeHoldEndXGrid is not null)
                    holdEnd.XGrid = beforeHoldEndXGrid;
            }));
        }

        public void KeyboardAction_DeleteSelectingObjects()
        {
            if (IsLocked)
                return;

            //删除已选择的物件
            var selectedObjectGroup = SelectObjects.OfType<OngekiObjectBase>().GroupBy(x => x.GetType()).ToArray();

            //特殊处理LaneCurvePathControlObject
            var cacheCurveControlsMap = new Dictionary<LaneCurvePathControlObject, (ConnectableChildObjectBase refObj, int idx)>();

            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.DeleteObjects, () =>
            {
                foreach (var group in selectedObjectGroup)
                {
                    if (group.Key == typeof(LaneCurvePathControlObject))
                    {
                        foreach (var controlObject in group.OfType<LaneCurvePathControlObject>())
                        {
                            var refObj = controlObject.RefCurveObject;
                            cacheCurveControlsMap[controlObject] = (refObj, refObj.PathControls.FirstIndexOf(x => x == controlObject));
                        }
                    }

                    RemoveObjects(group);
                }
            }, () =>
            {
                foreach (var group in selectedObjectGroup)
                {
                    if (group.Key == typeof(LaneCurvePathControlObject))
                    {
                        foreach (var item in group.OfType<LaneCurvePathControlObject>().Select(x =>
                        {
                            if (cacheCurveControlsMap.TryGetValue(x, out var val))
                                return (x, val.refObj, val.idx);
                            else
                                return default;
                        }).Where(x => x.x is not null).GroupBy(x => x.refObj))
                        {
                            var refObj = item.Key;
                            foreach ((var cp, _, var idx) in item.OrderBy(x => x.idx))
                                refObj.InsertControlObject(idx, cp);
                        }
                    }
                    else
                    {
                        group.ForEach(Fumen.AddObject);
                    }
                }
            }));
        }

        public void RemoveObjects(IEnumerable<OngekiObjectBase> objs)
        {
            foreach (var obj in objs)
            {
                if (obj is ISelectableObject selectable)
                    selectable.IsSelected = false;
                Fumen.RemoveObject(obj);
            }

            var propertyBrowser = IoC.Get<IFumenObjectPropertyBrowser>();
            if (IsActive)
                propertyBrowser.RefreshSelected(this);
        }

        public void RemoveObject(OngekiObjectBase obj) => RemoveObjects(obj.Repeat(1));

        public void KeyboardAction_SelectAllObjects()
        {
            if (IsLocked)
                return;

            Fumen.GetAllDisplayableObjects().OfType<ISelectableObject>().ForEach(x => x.IsSelected = true);
            IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(this);
        }

        public void KeyboardAction_CancelSelectingObjects()
        {
            if (IsLocked)
                return;

            //取消选择
            SelectObjects.ForEach(x => x.IsSelected = false);
        }

        public void KeyboardAction_FastSetObjectIsCritical(ActionExecutionContext e)
        {
            var propertyBrowser = IoC.Get<IFumenObjectPropertyBrowser>();

            var position = Mouse.GetPosition(e.View as FrameworkElement);
            position.Y = ViewHeight - position.Y + Rect.MinY;

            if (propertyBrowser.SelectedObjects.IsOnlyOne())
            {
                var child = propertyBrowser.SelectedObjects.FirstOrDefault() switch
                {
                    ConnectableChildObjectBase c => c,
                    LaneCurvePathControlObject cc => cc.RefCurveObject,
                    _ => default
                };
                if (child != null)
                {
                    //只有一个轨道Next被选择
                    ProcessAsAddCurve(child, position);
                    return;
                }
            }

            var selectables = SelectObjects.ToArray();
            var map = new Dictionary<ICriticalableObject, bool>();

            var isAllCritical = true;
            foreach (var selectable in selectables.OfType<ICriticalableObject>())
                isAllCritical &= map[selectable] = selectable.IsCritical;

            if (map.Count == 0)
            {
                ToastNotify(Resources.NoObjectCouldBeSetIsCritical);
                return;
            }

            var setVal = !isAllCritical;

            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.BatchSetIsCritical, () =>
            {
                foreach (var pair in map)
                    pair.Key.IsCritical = setVal;
            }, () =>
            {
                foreach (var pair in map)
                    pair.Key.IsCritical = pair.Value;
            }));
        }

        public bool CheckAndNotifyIfPlaceBeyondDuration(Point placePoint)
        {
            if (placePoint.Y > TotalDurationHeight || placePoint.Y < 0)
            {
                if (!EditorGlobalSetting.Default.EnablePlaceObjectBeyondAudioDuration)
                {
                    ToastNotify(Resources.DisableAddObjectBeyondAudioDuration);
                    return false;
                }
            }

            return true;
        }

        private void ProcessAsAddCurve(ConnectableChildObjectBase child, Point position)
        {
            if (!CheckAndNotifyIfPlaceBeyondDuration(position))
                return;

            var curvePoint = new LaneCurvePathControlObject();
            var dragTGrid = TGridCalculator.ConvertYToTGrid_DesignMode(position.Y, this);
            var dragXGrid = XGridCalculator.ConvertXToXGrid(position.X, this);
            var isFirst = true;

            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.AddCurveControlPoint, () =>
            {
                curvePoint.TGrid = dragTGrid;
                curvePoint.XGrid = dragXGrid;
                child.AddControlObject(curvePoint);
                if (isFirst)
                {
                    NotifyObjectClicked(curvePoint);
                    isFirst = false;
                }
            }, () =>
            {
                child.RemoveControlObject(curvePoint);
            }));
        }

        public void KeyboardAction_FastAddConnectableChild(ActionExecutionContext e)
        {
            if (!IsDesignMode)
                return;
            var propertyBrowser = IoC.Get<IFumenObjectPropertyBrowser>();


            var position = Mouse.GetPosition(e.View as FrameworkElement);
            position.Y = ViewHeight - position.Y + Rect.MinY;

            if (propertyBrowser.SelectedObjects.IsOnlyOne() && propertyBrowser.SelectedObjects.FirstOrDefault() is Hold hold)
            {
                //只有一个hold被选择，按下A那么就是添加HoldEnd
                ProcessAsHoldEnd(hold, position);
                return;
            }

            if (!propertyBrowser.SelectedObjects.All(x => x is ConnectableObjectBase))
                return;
            var selects = propertyBrowser.SelectedObjects.OfType<ConnectableObjectBase>().OrderBy(x => x.XGrid).ToArray();
            if (selects.IsEmpty())
                return;

            ProcessAsAddNextObjects(position, selects);
        }

        private void ProcessAsAddNextObjects(Point position, ConnectableObjectBase[] selects)
        {
            var minX = XGridCalculator.ConvertXGridToX(selects.Min(x => x.XGrid), this);
            var maxX = XGridCalculator.ConvertXGridToX(selects.Max(x => x.XGrid), this);
            var centerX = (maxX + minX) / 2;
            var xOffsetMap = selects
                .Select((x, i) => (XGridCalculator.ConvertXGridToX(x.XGrid, this) - centerX, i))
                .ToDictionary(x => x.i, x => x.Item1);

            var starts = selects.Select(x => x.ReferenceStartObject).Distinct().ToArray();

            if (starts.Length != selects.Length)
                return;

            var genChildren = new HashSet<ConnectableChildObjectBase>();

            UndoRedoManager.BeginCombineAction();
            foreach ((var start, var i) in starts.WithIndex())
            {
                var newPos = position;
                newPos.X = position.X + xOffsetMap[i];

                var genChild = start.CreateChildObject();
                var dropAction = new ConnectableObjectDropAction(start, genChild, () => { });
                dropAction.Drop(this, newPos);
                genChildren.Add(genChild);
            }
            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(string.Empty, () =>
            {
                TryCancelAllObjectSelecting();
                genChildren.ForEach(x => x.IsSelected = true);
                IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(this);
            }, () => { }));
            var combinedAction = UndoRedoManager.EndCombineAction(Resources.FastAddObjectsToLanes);
            UndoRedoManager.ExecuteAction(combinedAction);
        }

        private void ProcessAsHoldEnd(Hold hold, Point mousePosition)
        {
            var holdEnd = new HoldEnd();
            hold.SetHoldEnd(holdEnd);

            var isFirst = true;
            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.AddObject, () =>
            {
                MoveObjectTo(holdEnd, mousePosition);
                Fumen.AddObject(holdEnd);

                if (isFirst)
                {
                    NotifyObjectClicked(holdEnd);
                    isFirst = false;
                }
            }, () =>
            {
                RemoveObject(holdEnd);
            }));
        }

        public void KeyboardAction_PlayOrPause()
        {
            IoC.Get<IAudioPlayerToolViewer>().RequestPlayOrPause();
        }

        public void KeyboardAction_HideOrShow()
        {
            SwitchMode(!IsPreviewMode);
        }

        public void SwitchMode(bool isPreviewMode)
        {
            BulletPallete.RandomSeed = DateTime.Now.ToString().GetHashCode();

            var tGrid = GetCurrentTGrid();
            IsUserRequestHideEditorObject = isPreviewMode;
            convertToY = IsDesignMode ?
                TGridCalculator.ConvertTGridUnitToY_DesignMode :
                TGridCalculator.ConvertTGridUnitToY_PreviewMode;
            RecalculateTotalDurationHeight();
            ScrollTo(tGrid);
            var mousePos = Mouse.GetPosition(GetView() as FrameworkElement);
            UpdateCurrentCursorPosition(mousePos);
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
            var arg = e.EventArgs as MouseEventArgs;

            if (IsLocked)
                return;

            if ((e.View as FrameworkElement)?.Parent is not IInputElement parent)
                return;

            var pos = arg.GetPosition(parent);

            if (arg.RightButton == MouseButtonState.Released || prevRightButtonState == MouseButtonState.Pressed)
                contextMenuPosition = pos;
            prevRightButtonState = arg.RightButton;

            if (IsDesignMode)
            {
                if (isLeftMouseDown)
                {
                    if (IsRangeSelecting && SelectionCurrentCursorPosition != SelectionStartPosition)
                    {
                        SelectRangeObjects();
                    }
                    else
                    {
                        if (isSelectRangeDragging)
                        {
                            var cp = pos;
                            cp.Y = ViewHeight - cp.Y + Rect.MinY;
                            UndoRedoManager.BeginCombineAction();
                            SelectObjects.ToArray().ForEach(x =>
                            {
                                var obj = x as OngekiObjectBase;
                                InteractiveManager.GetInteractive(obj).OnDragEnd(obj, cp, this);
                            });
                            var compositeAction = UndoRedoManager.EndCombineAction(Resources.DragObjects);
                            UndoRedoManager.ExecuteAction(compositeAction);
                        }
                        else
                        {
                            //Log.LogDebug($"mouseDownHitObject = {mouseDownHitObject?.ReferenceOngekiObject}");
                            //if no object clicked or alt is pressing , just to process as brush actions.
                            if (mouseDownHitObject is null || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
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
                                    mouseDownHitObject = NotifyObjectClicked(mouseDownHitObject, mouseDownNextHitObject);
                            }
                        }
                    }

                    isLeftMouseDown = false;
                    isSelectRangeDragging = false;
                    SelectionVisibility = Visibility.Collapsed;
                    currentDraggingActionId = int.MaxValue;
                }

                if (isMiddleMouseDown)
                {
                    if (isCanvasDragging)
                    {
                        var diffX = pos.X - mouseCanvasStartPosition.X;
                        Setting.XOffset = startXOffset + diffX;

                        var curY = pos.Y;
                        var diffY = curY - mouseCanvasStartPosition.Y;

                        var audioTime = TGridCalculator.ConvertYToAudioTime_DesignMode(startScrollOffset + diffY, this);
                        //ScrollViewerVerticalOffset = Math.Max(0, Math.Min(TotalDurationHeight, startScrollOffset + diffY));
                        ScrollTo(audioTime);
                    }
                    else
                    {
                        Setting.XOffset = 0;
                    }

                    isCanvasDragging = false;
                    isMiddleMouseDown = false;
                }
            }
        }

        public void OnMouseDown(ActionExecutionContext e)
        {
            var arg = e.EventArgs as MouseEventArgs;

            prevRightButtonState = arg.RightButton;

            if (IsLocked || IsPreviewMode)
                return;

            var view = e.View as FrameworkElement;

            var position = arg.GetPosition(e.Source);

            if (arg.LeftButton == MouseButtonState.Pressed)
            {
                position.Y = Math.Max(0, Rect.MaxY - position.Y);

                isLeftMouseDown = true;
                isSelectRangeDragging = false;

                var hitResult = hits.AsParallel().Where(x => x.Value.Contains(position)).Select(x => x.Key).OrderBy(x => x.Id).ToList();
                if (TGridCalculator.ConvertYToTGrid_DesignMode(position.Y, this) is TGrid tGrid)
                {
                    var lanes = Fumen.Lanes.GetVisibleStartObjects(tGrid, tGrid).Select(start =>
                    {
                        var child = start.GetChildObjectFromTGrid(tGrid);
                        if (child?.CalulateXGrid(tGrid) is not XGrid xGrid)
                            return default;

                        var laneX = XGridCalculator.ConvertXGridToX(xGrid, this);
                        var diff = Math.Abs(laneX - position.X);
                        if (diff > 8)
                            return default;

                        return child as OngekiObjectBase;
                    }).FilterNull().OrderBy(x => x.Id);

                    hitResult = hitResult.Concat(lanes).Distinct().ToList();
                }
                if (BrushMode)
                {
                    //笔刷模式下，忽略点击线段和节点~
                    hitResult.RemoveAll(x => x is ConnectableObjectBase);
                }

                var idx = Math.Max(0, hitResult.IndexOf(mouseDownHitObject));
                var hitOngekiObject = hitResult.ElementAtOrDefault(idx);

                mouseDownHitObject = null;
                mouseDownNextHitObject = null;
                mouseDownHitObjectPosition = default;
                mouseSelectRangeStartPosition = position;
                dragOutBound = false;

                if (hitOngekiObject is null)
                {
                    TryCancelAllObjectSelecting();

                    //enable show selection

                    SelectionStartPosition = new Vector2((float)position.X, (float)position.Y);
                    SelectionCurrentCursorPosition = SelectionStartPosition;
                    SelectionVisibility = Visibility.Visible;
                }
                else
                {
                    //这里如果已经有物件选择了就判断是否还有其他物件可以选择
                    SelectionVisibility = Visibility.Collapsed;
                    mouseDownHitObject = hitOngekiObject;
                    mouseDownHitObjectPosition = position;

                    if (hitResult.Count > 1)
                    {
                        var nextIdx = (idx + 1) % hitResult.Count;
                        mouseDownNextHitObject = hitResult[nextIdx];
                    }
                }

                Log.LogDebug($"mousePos = （{position.X:F0},{position.Y:F0}) , hitOngekiObject = {hitOngekiObject} , mouseDownNextHitObject = {mouseDownNextHitObject}");
            }

            if (arg.MiddleButton == MouseButtonState.Pressed)
            {
                mouseCanvasStartPosition = position;
                startXOffset = Setting.XOffset;
                startScrollOffset = ScrollViewerVerticalOffset;

                isCanvasDragging = false;
                isMiddleMouseDown = true;
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

            if (!IsDesignMode)
                return;

            if (isMiddleMouseDown)
            {
                isCanvasDragging = true;

                var diffX = pos.X - mouseCanvasStartPosition.X;
                Setting.XOffset = startXOffset + diffX;

                var curY = pos.Y;
                var diffY = curY - mouseCanvasStartPosition.Y;

                var canvasY = startScrollOffset + diffY;
                var audioTime = TGridCalculator.ConvertYToAudioTime_DesignMode(canvasY, this);
                //ScrollViewerVerticalOffset = Math.Max(0, Math.Min(TotalDurationHeight, startScrollOffset + diffY));
                ScrollTo(audioTime);

                //Log.LogInfo($"diffY: {diffY:F2}  ScrollViewerVerticalOffset: {ScrollViewerVerticalOffset:F2}");
            }

            if (isLeftMouseDown)
            {
                var r = isSelectRangeDragging;
                isSelectRangeDragging = true;
                var dragCall = new Action<OngekiObjectBase, Point>((vm, pos) =>
                {
                    var action = InteractiveManager.GetInteractive(vm);
                    if (r)
                        action.OnDragMove(vm, pos, this);
                    else
                        action.OnDragStart(vm, pos, this);
                });

                var rp = 1 - pos.Y / ViewHeight;
                var srp = 1 - mouseSelectRangeStartPosition.Y / ViewHeight;
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

                var prev = CurrentPlayTime;
                var y = Rect.MinY + Setting.JudgeLineOffsetY + offsetY;

                //Log.LogDebug($"pos={pos.X:F2},{pos.Y:F2} offsetYAcc={offsetYAcc:F2} dragOutBound={dragOutBound} y={y:F2}");

                if (offsetY != 0)
                {
                    var audioTime = TGridCalculator.ConvertYToAudioTime_DesignMode(y, this);
                    ScrollTo(audioTime);
                }

                //检查判断，确定是拖动已选物品位置，还是说拉框选择区域
                if (IsRangeSelecting)
                {
                    //拉框
                    var p = pos;
                    p.Y = Math.Min(TotalDurationHeight, Math.Max(0, Rect.MaxY - p.Y + offsetY));
                    SelectionCurrentCursorPosition = new Vector2((float)p.X, (float)p.Y);
                }
                else
                {
                    //拖动已选物件
                    var cp = pos;
                    cp.Y = ViewHeight - cp.Y + Rect.MinY;
                    //Log.LogDebug($"SelectObjects: {SelectObjects.Count()}");
                    SelectObjects.ToArray().ForEach(x => dragCall(x as OngekiObjectBase, cp));
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
        }

        private void TryApplyBrushObject(Point p)
        {
            var copyManager = IoC.Get<IFumenEditorClipboard>();

            if (!(copyManager.CurrentCopiedObjects.IsOnlyOne(out var c) && c is OngekiObjectBase copySouceObj))
                return;

            var newObject = copySouceObj.CopyNew();
            if (newObject is null
                //不支持笔刷模式下新建以下玩意
                || newObject is ConnectableStartObject)
            {
                ToastNotify(Resources.NotSupportInBrushMode.Format(copySouceObj?.Name));
                return;
            }

            p.Y = ViewHeight - p.Y + Rect.MinY;
            var v = new Vector2((float)p.X, (float)p.Y);

            System.Action undo = () =>
            {
                if (newObject is ConnectableChildObjectBase childObject)
                {
                    (copySouceObj as ConnectableChildObjectBase)?.ReferenceStartObject.RemoveChildObject(childObject);
                }
                else
                {
                    RemoveObject(newObject);
                }
            };

            System.Action redo = async () =>
            {
                InteractiveManager.GetInteractive(newObject).OnMoveCanvas(newObject, p, this);
                var x = newObject is IHorizonPositionObject horizonPositionObject ? XGridCalculator.ConvertXGridToX(horizonPositionObject.XGrid, this) : 0;
                var y = newObject is ITimelineObject timelineObject ? TGridCalculator.ConvertTGridToY_DesignMode(timelineObject.TGrid, this) : 0;
                var dist = Vector2.Distance(v, new Vector2((float)x, (float)y));
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
                    if (newObject is ConnectableChildObjectBase childObject)
                    {
                        //todo there is a bug.
                        (copySouceObj as ConnectableChildObjectBase)?.ReferenceStartObject.AddChildObject(childObject);
                    }
                    else
                    {
                        Fumen.AddObject(newObject);
                    }
                }
            };

            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.AddObjectsByBrush, redo, undo));
        }

        #region Object Click&Selection

        public void TryCancelAllObjectSelecting(params ISelectableObject[] expects)
        {
            var objBrowser = IoC.Get<IFumenObjectPropertyBrowser>();
            expects = expects ?? new ISelectableObject[0];

            if (!(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || IsRangeSelecting || IsPreventMutualExclusionSelecting))
            {
                foreach (var o in SelectObjects.Where(x => !expects.Contains(x)))
                    o.IsSelected = false;
                objBrowser.RefreshSelected(this);
            }
        }

        public OngekiObjectBase NotifyObjectClicked(OngekiObjectBase obj, OngekiObjectBase next = default)
        {
            if (obj is not ISelectableObject selectable)
                return default;

            var objBrowser = IoC.Get<IFumenObjectPropertyBrowser>();
            var count = SelectObjects.Take(2).Count();
            var first = SelectObjects.FirstOrDefault();

            if ((count > 1) || (count == 1 && first != obj)) //比如你目前有多个已选择的，但你单点了一个
            {
                TryCancelAllObjectSelecting(obj as ISelectableObject);
                selectable.IsSelected = true;
            }
            else
            {
                selectable.IsSelected = !selectable.IsSelected;
                TryCancelAllObjectSelecting(obj as ISelectableObject);

                if (next != null && !selectable.IsSelected)
                    return NotifyObjectClicked(next);
            }

            objBrowser.RefreshSelected(this);
            IoC.Get<IShell>().ActiveLayoutItem = this;
            return obj;
        }

        #endregion

        private void RecalculateSelectionRect()
        {
            var sx = Math.Min(SelectionStartPosition.X, SelectionCurrentCursorPosition.X);
            var sy = Math.Min(SelectionStartPosition.Y, SelectionCurrentCursorPosition.Y);

            var ex = Math.Max(SelectionStartPosition.X, SelectionCurrentCursorPosition.X);
            var ey = Math.Max(SelectionStartPosition.Y, SelectionCurrentCursorPosition.Y);

            var width = Math.Abs(sx - ex);
            var height = Math.Abs(sy - ey);

            SelectionRect = new Rect(sx, sy, width, height);

            //Log.LogDebug($"SelectionRect = {SelectionRect}");
        }

        private void UpdateCurrentCursorPosition(Point pos)
        {
            var contentObject = IoC.Get<CommonStatusBar>().SubRightMainContentViewModel;

            var canvasY = Rect.MaxY - pos.Y;
            var canvasX = pos.X;
            CurrentCursorPosition = new(canvasX, canvasY);

            var tGrid = default(TGrid);
            if (IsDesignMode)
                tGrid = TGridCalculator.ConvertYToTGrid_DesignMode(canvasY, this);
            else
            {
                var result = TGridCalculator.ConvertYToTGrid_PreviewMode(canvasY, this);
                if (result.IsOnlyOne())
                    tGrid = result.FirstOrDefault();
            }
            TimeSpan? audioTime = tGrid is not null ? TGridCalculator.ConvertTGridToAudioTime(tGrid, this) : null;
            var xGrid = XGridCalculator.ConvertXToXGrid(canvasX, this);
            contentObject.Message = $"C[{canvasX:F2},{canvasY:F2}] {(tGrid is not null ? $"T[{tGrid.Unit},{tGrid.Grid}]" : "T[N/A]")} X[{xGrid.Unit:F2},{xGrid.Grid}] A[{audioTime?.ToString("mm\\:ss\\.fff") ?? "N/A"}]";
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
            if (!IsDesignMode)
            {
                Log.LogWarn($"请先将编辑器切换到编辑模式");
                return;
            }

            var arg = e.EventArgs as DragEventArgs;
            if (arg.Data.GetDataPresent(ToolboxDragDrop.DataFormat))
            {
                var mousePosition = arg.GetPosition(e.View as FrameworkElement);
                mousePosition.Y = ViewHeight - mousePosition.Y + Rect.MinY;

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
        }

        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RegisterSelectableObject(OngekiObjectBase obj, Vector2 centerPos, Vector2 size)
        {
            //rect.Y = rect.Y - CurrentPlayTime;
            hits[obj] = new Rect(centerPos.X - size.X / 2, centerPos.Y - size.Y / 2, size.X, size.Y);
        }

        private void OnWheelScrollViewer(MouseWheelEventArgs arg)
        {
            if (IsDesignMode && Setting.JudgeLineAlignBeat)
            {
                var tGrid = GetCurrentTGrid();
                var time = TGridCalculator.ConvertTGridToAudioTime(tGrid, this);
                var y = TGridCalculator.ConvertTGridToY_DesignMode(tGrid, this);

                var timeSignatures = Fumen.MeterChanges.GetCachedAllTimeSignatureUniformPositionList(Fumen.BpmList);
                (var prevAudioTime, _, var meter, var bpm) = timeSignatures.LastOrDefault(x => x.audioTime < time);
                if (meter is null)
                    (prevAudioTime, _, meter, bpm) = timeSignatures.FirstOrDefault();

                var nextY = ScrollViewerVerticalOffset + TGridCalculator.CalculateOffsetYPerBeat(bpm, meter, Setting.BeatSplit, Setting.VerticalDisplayScale) * 2;
                //消除精度误差~
                var prevY = Math.Max(0, TGridCalculator.ConvertAudioTimeToY_DesignMode(prevAudioTime, this) - 1);

                var downs = TGridCalculator.GetVisbleTimelines_DesignMode(Fumen.Soflans, Fumen.BpmList, Fumen.MeterChanges, prevY, ScrollViewerVerticalOffset, 0, Setting.BeatSplit, Setting.VerticalDisplayScale);
                var downFirst = downs.Where(x => x.tGrid != tGrid).LastOrDefault();
                var nexts = TGridCalculator.GetVisbleTimelines_DesignMode(Fumen.Soflans, Fumen.BpmList, Fumen.MeterChanges, ScrollViewerVerticalOffset, nextY, 0, Setting.BeatSplit, Setting.VerticalDisplayScale);
                var nextFirst = nexts.Where(x => x.tGrid != tGrid).FirstOrDefault();

                var result = arg.Delta > 0 ? nextFirst : downFirst;
                if (result.tGrid is not null)
                {
                    var audioTime = TGridCalculator.ConvertYToAudioTime_DesignMode(result.y, this);
                    ScrollTo(audioTime);
                    //ScrollTo(result.y);
                }
            }
            else
            {
                if (IsPreviewMode)
                {
                    var audioTime = TGridCalculator.ConvertTGridToAudioTime(GetCurrentTGrid(), this);
                    var offset = TimeSpan.FromMilliseconds(Setting.MouseWheelLength);
                    if (Math.Sign(arg.Delta) > 0)
                        audioTime += offset;
                    else
                        audioTime -= offset;
                    ScrollTo(audioTime);
                }
                else
                {
                    var y = ScrollViewerVerticalOffset + Math.Sign(arg.Delta) * Setting.MouseWheelLength;
                    y = Math.Max(Math.Min(y, TotalDurationHeight), 0);
                    var audioTime = TGridCalculator.ConvertYToAudioTime_DesignMode(y, this);
                    ScrollTo(audioTime);
                    //ScrollTo(ScrollViewerVerticalOffset + Math.Sign(arg.Delta) * Setting.MouseWheelLength);
                }
            }
        }

        public void OnMouseWheel(ActionExecutionContext e)
        {
            if (IsLocked)
                return;

            var arg = e.EventArgs as MouseWheelEventArgs;
            arg.Handled = true;

            if (Keyboard.IsKeyDown(Key.LeftAlt))
                OnWheelBeatSplit(arg);
            else if (Keyboard.IsKeyDown(Key.LeftShift))
                OnWheelXGridUnit(arg);
            else
                OnWheelScrollViewer(arg);
        }

        /*
         0:            7       11    13 14
         3:     3     6    9      12
         2: 1 2   4      8  
         5:         5        10            
         */
        private readonly static int[] xGridUnitUpJumpTable = new[] { 0, 1, 2, 3, 4, 5, 3, 7, 0, 3, 0, 0, 0, 0, 0, 0 };
        private readonly static int[] xGridUnitDownJumpTable = new[] { 0, 0, -1, 0, -2, 0, -3, 0, -4, -3, -5, 0, -3, 0, 0, 0 };
        private void OnWheelXGridUnit(MouseWheelEventArgs arg)
        {
            var jump = (arg.Delta > 0 ? xGridUnitUpJumpTable : xGridUnitDownJumpTable).ElementAtOrDefault((int)Setting.XGridUnitSpace);
            if (jump == 0)
                return;

            var newVal = jump + Setting.XGridUnitSpace;
            if (newVal != 0 && newVal <= 16)
                Setting.XGridUnitSpace = newVal;
        }

        /*
         0:                    11    13
         3:     3     6    9      12
         2: 1 2   4      8  
         5:         5        10            15
         7:            7                14
         */
        private readonly static int[] beatSplitUpJumpTable = new[] { 0, 1, 2, 3, 4, 5, 3, 7, 0, 3, 5, 0, 0, 0, 0, 0 };
        private readonly static int[] beatSplitDownJumpTable = new[] { 0, 0, -1, 0, -2, 0, -3, 0, -4, -3, -5, 0, -3, 0, -7, -5 };
        private void OnWheelBeatSplit(MouseWheelEventArgs arg)
        {
            var jump = (arg.Delta > 0 ? beatSplitUpJumpTable : beatSplitDownJumpTable).ElementAtOrDefault(Setting.BeatSplit);
            if (jump == 0)
                return;

            var newVal = jump + Setting.BeatSplit;
            if (newVal != 0 && newVal <= 16)
                Setting.BeatSplit = newVal;
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
            IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(this);
            ToastNotify(Resources.EditorLock);
        }

        /// <summary>
        /// 接触对编辑器用户操作的封锁
        /// </summary>
        public void UnlockAllUserInteraction()
        {
            if (!IsLocked)
                return;
            IsLocked = false;
            ToastNotify(Resources.EditorUnlock);
        }

        #endregion

        public void ToastNotify(string message)
        {
            Toast?.ShowMessage(message);
            Log.LogInfo(message);
        }

        #region Object Interaction

        public void MoveObjectTo(OngekiObjectBase obj, Point point) => InteractiveManager.GetInteractive(obj).OnMoveCanvas(obj, point, this);

        #endregion
    }
}
