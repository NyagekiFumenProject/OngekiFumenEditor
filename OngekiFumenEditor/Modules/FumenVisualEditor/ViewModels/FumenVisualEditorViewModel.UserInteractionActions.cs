using Caliburn.Micro;
using DereTore.Common;
using Gemini.Framework;
using Gemini.Framework.Services;
using Gemini.Modules.Toolbox;
using Gemini.Modules.Toolbox.Models;
using Gemini.Modules.UndoRedo;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
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
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Gemini.Framework.Commands;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using System.Threading;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.BatchModeToggle;

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

        public readonly SelectionArea SelectionArea;

        public bool IsRangeSelecting => SelectionArea.IsActive;
        public bool IsPreventMutualExclusionSelecting { get; set; }

        public void ConsumeSelectionArea()
        {
            if (!SelectionArea.IsActive)
                return;

            SelectionArea.ApplyRangeAction();
            SelectionArea.IsActive = false;
        }

        public void ClearSelection()
        {
            foreach (var obj in SelectObjects)
            {
                obj.IsSelected = false;
            }
        }

        public void AddToSelection(OngekiMovableObjectBase obj)
        {
            obj.IsSelected = true;
            NotifyObjectClicked(obj);
            Log.LogInfo(obj.IsSelected.ToString());
        }

        public void ReplaceSelection(OngekiMovableObjectBase obj)
        {
            ClearSelection();
            AddToSelection(obj);
        }

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

        public ImmutableDictionary<OngekiObjectBase, Rect> GetHits() => hits.ToImmutableDictionary();

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

        public void MenuItemAction_SelectAll(ActionExecutionContext e)
        {
            IsPreventMutualExclusionSelecting = true;

            Fumen.GetAllDisplayableObjects().OfType<ISelectableObject>().ForEach(x => x.IsSelected = true);
            IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(Editor);

            IsPreventMutualExclusionSelecting = false;
        }

        public void MenuItemAction_ReverseSelect(ActionExecutionContext e)
        {
            IsPreventMutualExclusionSelecting = true;

            Fumen.GetAllDisplayableObjects().OfType<ISelectableObject>().ForEach(x => x.IsSelected = !x.IsSelected);
            IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(Editor);

            IsPreventMutualExclusionSelecting = false;
        }

        public async void MenuItemAction_CopySelectedObjects(ActionExecutionContext e)
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

        public void MenuItemAction_PasteCopiesObjects(ActionExecutionContext ctx)
            => KeyboardAction_PasteCopiesObjects(ctx);

        public void KeyboardAction_PasteCopiesObjects(ActionExecutionContext e)
        {
            var placePos = Mouse.GetPosition(GetView() as FrameworkElement);
            placePos.Y = ViewHeight - placePos.Y + Rect.MinY;
            PasteCopiesObjects(PasteOption.None, placePos);
        }

        public void MenuItemAction_PasteCopiesObjectsDirectly(ActionExecutionContext ctx)
        {
            PasteCopiesObjects(PasteOption.Direct, default(Point));
        }

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

        public void MenuItemAction_MirrorSelectionXGridZero(ActionExecutionContext ctx)
        {
            var selection = SelectObjects.OfType<OngekiMovableObjectBase>().ToList();
            if (selection.Count == 0)
            {
                return;
            }

            var func = () => MirrorObjectsXGrid(selection, true);
            UndoRedoManager.ExecuteAction(new LambdaUndoAction(Resources.MirrorSelectionXGridZero, func, func));
        }

        public void MenuItemAction_MirrorSelectionXGrid(ActionExecutionContext ctx)
        {
            var selection = SelectObjects.OfType<OngekiMovableObjectBase>().ToList();
            if (selection.Count == 0)
            {
                return;
            }

            var func = () => MirrorObjectsXGrid(selection, false);
            UndoRedoManager.ExecuteAction(new LambdaUndoAction(Resources.MirrorSelectionXGrid, func, func));
        }

        private void MirrorObjectsXGrid(IList<OngekiMovableObjectBase> objects, bool zeroCenter)
        {
            int center;
            if (zeroCenter)
            {
                center = 0;
            }
            else
            {
                var selectionBounds = objects.Aggregate((int.MaxValue, int.MinValue), (bounds, o) =>
                {
                    var min = bounds.Item1;
                    var max = bounds.Item2;

                    if (o.XGrid.TotalGrid < min)
                    {
                        min = o.XGrid.TotalGrid;
                    }

                    if (o.XGrid.TotalGrid > max)
                    {
                        max = o.XGrid.TotalGrid;
                    }

                    return (min, max);
                });
                center = (selectionBounds.MinValue + selectionBounds.MaxValue) / 2;
            }

            foreach (var obj in objects)
            {
                var diff = obj.XGrid.TotalGrid - center;
                obj.XGrid = XGrid.FromTotalGrid(center - diff);
            }
        }

        public void MenuItemAction_MirrorLaneColors(ActionExecutionContext ctx)
        {
            var laneObjects = SelectObjects.OfType<ConnectableStartObject>()
                .Where(o => o.IsDockableLane)
                .Where(o => o.Children.All(c => c.IsSelected))
                .ToList();

            if (laneObjects.Count == 0)
            {
                return;
            }

            List<ConnectableStartObject> newLaneObjects = null;
            var executeOrRedo = () =>
            {
                if (newLaneObjects is null)
                {
                    newLaneObjects = MirrorLaneColors(laneObjects).ToList();
                }
                else
                {
                    Fumen.RemoveObjects(laneObjects);
                    Fumen.AddObjects(newLaneObjects);
                }
                newLaneObjects.ForEach(SelectLaneObjects);
            };

            var undo = () =>
            {
                Fumen.RemoveObjects(newLaneObjects!);
                Fumen.AddObjects(laneObjects);
                laneObjects.ForEach(SelectLaneObjects);
            };

            UndoRedoManager.ExecuteAction(new LambdaUndoAction(Resources.MirrorSelectionLaneColors, executeOrRedo, undo));
        }

        private IEnumerable<ConnectableStartObject> MirrorLaneColors(List<ConnectableStartObject> laneObjects)
        {
            foreach (var obj in laneObjects)
            {
                LaneStartBase startNode = obj.LaneType switch
                {
                    LaneType.Left => new LaneRightStart(),
                    LaneType.Right => new LaneLeftStart(),
                    LaneType.WallLeft => new WallRightStart(),
                    LaneType.WallRight => new WallLeftStart(),
                    _ => null
                };
                if (startNode is null)
                    continue;

                startNode.XGrid = obj.XGrid;
                startNode.TGrid = obj.TGrid;
                startNode.Tag = obj.Tag;

                foreach (var child in obj.Children)
                {
                    var newChild = startNode.CreateChildObject();
                    newChild.XGrid = child.XGrid;
                    newChild.TGrid = child.TGrid;
                    newChild.Tag = child.Tag;
                    newChild.CurvePrecision = child.CurvePrecision;

                    foreach (var curvePoint in child.PathControls)
                    {
                        newChild.AddControlObject(new LaneCurvePathControlObject()
                        {
                            RefCurveObject = newChild,
                            XGrid = curvePoint.XGrid,
                            TGrid = curvePoint.TGrid,
                            Tag = curvePoint.Tag,
                        });
                    }

                    startNode.AddChildObject(newChild);
                }

                foreach (var tap in Fumen.Taps.Where(t => t.ReferenceLaneStart == obj))
                {
                    tap.ReferenceLaneStart = startNode;
                }

                foreach (var hold in Fumen.Holds.Where(h => h.ReferenceLaneStart == obj))
                {
                    hold.ReferenceLaneStart = startNode;
                }

                Fumen.RemoveObject(obj);
                Fumen.AddObject(startNode);
                yield return startNode;
            }
        }

        private void SelectLaneObjects(ConnectableStartObject start)
        {
            start.IsSelected = true;
            start.Children.ForEach(c => c.IsSelected = true);
        }

        private Dictionary<ITimelineObject, double> cacheObjectAudioTime = new();
        private OngekiObjectBase mouseDownHitObject;
        private Point? mouseDownHitObjectPosition;
        /// <summary>
        /// 表示指针是否出拖动出滚动范围
        /// </summary>
        private bool dragOutBound;
        private int currentDraggingActionId;

        public void MenuItemAction_RememberSelectedObjectAudioTime(ActionExecutionContext e)
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

        public void MenuItemAction_RecoverySelectedObjectToAudioTime(ActionExecutionContext e)
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

        public void KeyboardAction_FastPlaceDockableObjectToCenter(ActionExecutionContext e)
            => KeyboardAction_FastPlaceDockableObject(LaneType.Center);
        public void KeyboardAction_FastPlaceDockableObjectToLeft(ActionExecutionContext e)
            => KeyboardAction_FastPlaceDockableObject(LaneType.Left);
        public void KeyboardAction_FastPlaceDockableObjectToRight(ActionExecutionContext e)
            => KeyboardAction_FastPlaceDockableObject(LaneType.Right);
        public void KeyboardAction_FastPlaceDockableObjectToWallLeft(ActionExecutionContext e)
            => KeyboardAction_FastPlaceDockableObject(LaneType.WallLeft);
        public void KeyboardAction_FastPlaceDockableObjectToWallRight(ActionExecutionContext e)
            => KeyboardAction_FastPlaceDockableObject(LaneType.WallRight);

        public void KeyboardAction_FastPlaceNewTap(ActionExecutionContext e)
        {
            var position = Mouse.GetPosition(e.View as FrameworkElement);
            position.Y = ViewHeight - position.Y + Rect.MinY;

            KeyboardAction_FastPlaceNewObject<Tap>(position);
        }

        public void KeyboardAction_FastPlaceNewHold(ActionExecutionContext e)
        {
            var position = Mouse.GetPosition(e.View as FrameworkElement);
            position.Y = ViewHeight - position.Y + Rect.MinY;

            KeyboardAction_FastPlaceNewObject<Hold>(position);
        }

        public void KeyboardAction_ChangeDockableLaneType(ActionExecutionContext e)
        {
            if (!(
                SelectObjects.IsOnlyOne(out var r) &&
                r is ConnectableObjectBase connectable &&
                connectable.ReferenceStartObject is LaneStartBase start &&
                start.IsDockableLane))
            {
                ToastNotify(Resources.SelectOneDockableLaneOnly);
                return;
            }

            LaneStartBase genStart = start.LaneType switch
            {
                LaneType.Left => new LaneCenterStart(),
                LaneType.Center => new LaneRightStart(),
                LaneType.Right => new LaneLeftStart(),

                LaneType.WallRight => new WallLeftStart(),
                LaneType.WallLeft => new WallRightStart(),

                _ => throw new NotSupportedException(),
            };

            void CopyCommon(ConnectableObjectBase s, ConnectableObjectBase t)
            {
                t.TGrid = s.TGrid;
                t.XGrid = s.XGrid;
            }

            void CopyChild(ConnectableChildObjectBase s, ConnectableChildObjectBase t)
            {
                CopyCommon(s, t);

                t.CurveInterpolaterFactory = s.CurveInterpolaterFactory;
                t.CurvePrecision = s.CurvePrecision;
                foreach (var ctrl in s.PathControls)
                {
                    var cp = (LaneCurvePathControlObject)ctrl.CopyNew();
                    cp.TGrid = cp.TGrid;
                    cp.XGrid = cp.XGrid;
                    t.AddControlObject(cp);
                }
            }

            //generate and setup new lane.
            CopyCommon(start, genStart);
            foreach (var child in start.Children)
            {
                var cpChild = genStart.CreateChildObject();
                CopyChild(child, cpChild);
                genStart.AddChildObject(cpChild);
            }

            var affactedDockableObjects = Fumen.GetAllDisplayableObjects()
                .OfType<ILaneDockable>()
                .Where(x => x.ReferenceLaneStart == start)
                .ToArray();

            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.kbd_editor_ChangeDockableLaneType, () =>
            {
                RemoveObject(start);
                Fumen.AddObject(genStart);
                NotifyObjectClicked(genStart);

                foreach (var obj in affactedDockableObjects)
                    obj.ReferenceLaneStart = genStart;
            }, () =>
            {
                RemoveObject(genStart);
                Fumen.AddObject(start);
                NotifyObjectClicked(start);

                foreach (var obj in affactedDockableObjects)
                    obj.ReferenceLaneStart = start;
            }));
        }

        private void KeyboardAction_FastPlaceNewObject<T>(Point position) where T : OngekiObjectBase, new()
        {
            var tap = new T();
            var isFirst = true;
            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("Add {0}".Format(typeof(T).Name), () =>
            {
                MoveObjectTo(tap, position);
                Fumen.AddObject(tap);

                if (isFirst)
                {
                    NotifyObjectClicked(tap);
                    isFirst = false;
                }
            }, () =>
            {
                RemoveObject(tap);
            }));
        }

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

        public void KeyboardAction_DeleteSelectingObjects(ActionExecutionContext e)
        {
            DeleteSelection();
        }

        public void DeleteSelection(IEnumerable<OngekiObjectBase> selection = null)
        {
            if (IsLocked)
                return;

            //获取要删除的物件
            var selects = selection?.ToArray() ?? SelectObjects.OfType<OngekiObjectBase>().ToArray();

            //依附于其他对象的子物件，比如轨道节点，曲线控制节点，无法做到单纯删除和添加
            //记录它们子节点相对于集合的位置，下次恢复的时候就是插入了
            var curveControlMaps = selects
                .OfType<LaneCurvePathControlObject>()
                .GroupBy(x => x.RefCurveObject)
                .ToDictionary(x => x.Key, x => x.Select(c => (c, x.Key.PathControls.FirstIndexOf(p => p == c))).OrderBy(x => x.Item2).ToArray());
            var connectablesMaps = selects
                .OfType<ConnectableChildObjectBase>()
                .GroupBy(x => x.ReferenceStartObject)
                .ToDictionary(x => x.Key, x => x.Select(c => (c, x.Key.Children.FirstIndexOf(p => p == c))).OrderBy(x => x.Item2).ToArray());

            var expectedObjects = selects.Where(x => x switch
            {
                LaneCurvePathControlObject => false,
                ConnectableChildObjectBase => false,
                _ => true
            });

            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.DeleteObjects, () =>
            {
                RemoveObjects(selects);
            }, () =>
            {
                expectedObjects.ForEach(Fumen.AddObject);

                foreach (var item in curveControlMaps)
                {
                    var child = item.Key;
                    foreach (var (curve, idx) in item.Value)
                    {
                        child.InsertControlObject(idx, curve);
                    }
                }

                foreach (var item in connectablesMaps)
                {
                    var start = item.Key;
                    foreach (var (child, idx) in item.Value)
                    {
                        start.InsertChildObject(idx, child);
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

        public void KeyboardAction_SelectAllObjects(ActionExecutionContext e)
        {
            if (IsLocked)
                return;

            Fumen.GetAllDisplayableObjects().OfType<ISelectableObject>().ForEach(x => x.IsSelected = true);
            IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(this);
        }

        public void KeyboardAction_CancelSelectingObjects(ActionExecutionContext e)
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

        public void KeyboardAction_FastSwitchFlickDirection(ActionExecutionContext _)
        {
            var selectedFlicks = SelectObjects.OfType<Flick>().ToList();

            if (selectedFlicks.Count == 0)
            {
                ToastNotify(Resources.NoFlickCouldBeSwitched);
                return;
            }

            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.BatchSwitchFlickDirection, ChangeFlicks, ChangeFlicks));

            void ChangeFlicks()
            {
                foreach (var flick in selectedFlicks)
                    flick.Direction = flick.Direction == Flick.FlickDirection.Left
                        ? Flick.FlickDirection.Right
                        : Flick.FlickDirection.Left;
            }
        }

        public void KeyboardAction_ToggleBatchMode(ActionExecutionContext ctx)
        {
            var command = IoC.Get<ICommandService>().GetCommand(new BatchModeToggleCommandDefinition());
            CommandRouterHelper.ExecuteCommand(command).Wait();
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
            var isFirst = true;

            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.AddCurveControlPoint, () =>
            {
                child.AddControlObject(curvePoint);
                MoveObjectTo(curvePoint, position);
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

        public void KeyboardAction_PlayOrPause(ActionExecutionContext e)
        {
            IoC.Get<IAudioPlayerToolViewer>().RequestPlayOrPause();
        }

        public void KeyboardAction_HideOrShow(ActionExecutionContext e)
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
            var arg = e.EventArgs as MouseButtonEventArgs;
            if (arg is null || arg.Handled)
            {
                return;
            }

            Log.LogInfo("Visual mouseup");

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
                if (arg.ChangedButton == MouseButton.Left)
                {
                    if (SelectionArea.IsActive && !SelectionArea.IsClick())
                    {
                        ConsumeSelectionArea();
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
                            if (mouseDownHitObject is not null)
                            {
                                if (mouseDownHitObjectPosition is Point p)
                                    mouseDownHitObject = NotifyObjectClicked(mouseDownHitObject, mouseDownNextHitObject);
                            }
                        }
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
                    }

                    isSelectRangeDragging = false;
                    currentDraggingActionId = int.MaxValue;
                    SelectionArea.IsActive = false;
                }

                if (arg.ChangedButton == MouseButton.Middle)
                {
                    isCanvasDragging = false;
                    isMiddleMouseDown = false;
                }
            }
            else
            {
                if (isDraggingPlayerLocation)
                {
                    Log.LogDebug("release playerLocation dragging");
                    isDraggingPlayerLocation = false;
                }
            }
        }

        public void OnMouseDown(ActionExecutionContext e)
        {
            if (e.EventArgs is not MouseButtonEventArgs arg || arg.Handled)
            {
                return;
            }
            Log.LogInfo("Visual mousedown");

            prevRightButtonState = arg.RightButton;

            var view = e.View as FrameworkElement;
            var position = arg.GetPosition(e.Source);

            if (IsDesignMode)
            {
                if (IsLocked)
                    return;

                if (arg.ChangedButton == MouseButton.Left)
                {
                    position.Y = Math.Max(0, Rect.MaxY - position.Y);

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

                    var idx = Math.Max(0, hitResult.IndexOf(mouseDownHitObject));
                    var hitOngekiObject = hitResult.ElementAtOrDefault(idx);

                    mouseDownHitObject = null;
                    mouseDownNextHitObject = null;
                    mouseDownHitObjectPosition = default;
                    dragOutBound = false;

                    if (hitOngekiObject is null)
                    {
                        TryCancelAllObjectSelecting();
                        Log.LogInfo($"SelectionArea ${CurrentCursorPosition}");

                        if (!SelectionArea.IsActive)
                        {
                            InitializeSelectionArea(SelectionAreaKind.Select);
                        }
                    }
                    else
                    {
                        //这里如果已经有物件选择了就判断是否还有其他物件可以选择
                        SelectionArea.IsActive = false;
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

                if (arg.ChangedButton == MouseButton.Middle)
                {
                    mouseCanvasStartPosition = position;
                    startXOffset = Setting.XOffset;
                    startScrollOffset = ScrollViewerVerticalOffset;

                    isCanvasDragging = false;
                    isMiddleMouseDown = true;
                }
            }
            else
            {
                if (arg.ChangedButton == MouseButton.Left && EditorGlobalSetting.Default.EnableShowPlayerLocation)
                {
                    //check if is dragging playerlocation

                    var y = TGridCalculator.ConvertAudioTimeToY_PreviewMode(CurrentPlayTime, this);
                    var x = XGridCalculator.ConvertXGridToX(PlayerLocationRecorder.GetLocationXUnit(CurrentPlayTime), this);

                    var mouseX = position.X;
                    var mouseY = -position.Y + Rect.MaxY;

                    Log.LogDebug($"playerLoc:({x:F2},{y:F2})  mouse:({mouseX:F2},{mouseY:F2})");
                    if (Math.Abs(mouseY - y) <= 48 && Math.Abs(mouseX - x) <= 48)
                    {
                        //click player location
                        isDraggingPlayerLocation = true;
                        draggingPlayerLocationCurrentX = XGridCalculator.ConvertXToXGridTotalUnit(mouseX, this);
                    }
                }
            }

            (e.View as FrameworkElement)?.Focus();
        }

        public void InitializeSelectionArea(SelectionAreaKind kind, Point? position = null)
        {
            var cursor = position ?? CurrentCursorPosition!.Value;

            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                TryCancelAllObjectSelecting();
            }

            SelectionArea.SelectionAreaKind = kind;
            SelectionArea.IsActive = true;
            SelectionArea.FilterFunc = null;
            SelectionArea.StartPoint = cursor;
            SelectionArea.EndPoint = cursor;
        }

        public void OnMouseMove(ActionExecutionContext e)
        {
            if (e.EventArgs is not MouseEventArgs args || args.Handled)
            {
                return;
            }

            if ((e.View as FrameworkElement)?.Parent is not IInputElement parent)
                return;
            currentDraggingActionId = int.MaxValue;
            OnMouseMove(args.GetPosition(parent));
        }

        public async void OnMouseMove(Point pos)
        {
            //show current cursor position in statusbar
            UpdateCurrentCursorPosition(pos);

            if (IsLocked)
                return;

            if (IsDesignMode)
            {
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

                if (SelectionArea.IsActive)
                {
                    var rp = 1 - pos.Y / ViewHeight;
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
                    var y = Rect.MinY + Setting.JudgeLineOffsetY + offsetY;

                    if (offsetY != 0)
                    {
                        var audioTime = TGridCalculator.ConvertYToAudioTime_DesignMode(y, this);
                        ScrollTo(audioTime);

                        var currentid = currentDraggingActionId = MathUtils.Random(int.MaxValue - 1);
                        await Task.Delay(1000 / 60);
                        if (currentDraggingActionId == currentid)
                            OnMouseMove(pos);
                    }

                    //拉框
                    var p = pos;
                    p.Y = Math.Min(TotalDurationHeight, Math.Max(0, Rect.MaxY - p.Y + offsetY));
                    SelectionArea.EndPoint = p;
                }

                if (Mouse.LeftButton == MouseButtonState.Pressed)
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

                    if (EnableDragging && !IsRangeSelecting)
                    {
                        //拖动已选物件
                        var cp = pos;
                        cp.Y = ViewHeight - cp.Y + Rect.MinY;
                        //Log.LogDebug($"SelectObjects: {SelectObjects.Count()}");
                        SelectObjects.ToArray().ForEach(x => dragCall(x as OngekiObjectBase, cp));
                    }
                }
            }
            else
            {
                //preview mode
                if (isDraggingPlayerLocation)
                {
                    //update current dragging player location
                    draggingPlayerLocationCurrentX = XGridCalculator.ConvertXToXGridTotalUnit(pos.X, this);
                }
            }
        }


        #region Quick Add Actions

        public void KeyboardAction_AddNewWallLeft(bool clearSelection = true)
            => TryCreateObjectAtMouse(new WallLeftStart(), clearSelection);
        public void KeyboardAction_AddNewWallRight(bool clearSelection = true)
            => TryCreateObjectAtMouse(new WallRightStart(), clearSelection);
        public void KeyboardAction_AddNewLaneLeft(bool clearSelection = true)
            => TryCreateObjectAtMouse(new LaneLeftStart(), clearSelection);
        public void KeyboardAction_AddNewLaneCenter(bool clearSelection = true)
            => TryCreateObjectAtMouse(new LaneCenterStart(), clearSelection);
        public void KeyboardAction_AddNewLaneRight(bool clearSelection = true)
            => TryCreateObjectAtMouse(new LaneRightStart(), clearSelection);
        public void KeyboardAction_AddNewLaneColorful(bool clearSelection = true)
            => TryCreateObjectAtMouse(new ColorfulLaneStart(), clearSelection);

        public void KeyboardAction_AddNewFlick(bool switchDirection = false, bool clearSelection = false)
            => TryCreateObjectAtMouse(new Flick() { Direction = switchDirection ? Flick.FlickDirection.Right : Flick.FlickDirection.Left }, clearSelection);

        public void KeyboardAction_AddNewBlock(bool switchDirection = false, bool clearSelection = false)
            => TryCreateObjectAtMouse(
                new LaneBlockArea()
                {
                    Direction = switchDirection ? LaneBlockArea.BlockDirection.Right : LaneBlockArea.BlockDirection.Left
                }, clearSelection);

        public void KeyboardAction_AddNewTap(bool clearSelection = false)
        {
            TryCreateObjectAtMouse(new Tap(), clearSelection, false);
        }

        #endregion

        private void TryCreateObjectAtMouse(OngekiObjectBase obj, bool clearSelection, bool autoSelectObj = true)
        {
            if (!CurrentCursorPosition.HasValue)
            {
                return;
            }

            MoveObjectTo(obj, CurrentCursorPosition.Value);
            Fumen.AddObject(obj);
            InteractiveManager.GetInteractive(obj).OnMoveCanvas(obj, CurrentCursorPosition.Value, this);

            if (clearSelection)
            {
                SelectObjects.ForEach(o => o.IsSelected = false);
            }

            if (autoSelectObj)
            {
                ((ISelectableObject)obj).IsSelected = true;
                NotifyObjectClicked(obj);
            }
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

        public void ScrollPage(int page)
        {
            TGrid changeGrid;

            if (!IsDesignMode && TGridCalculator.ConvertYToTGrid_PreviewMode(ViewHeight, this).ToList() is [{ } single])
            {
                changeGrid = single;
            }
            else
            {
                changeGrid = TGridCalculator.ConvertYToTGrid_DesignMode(ViewHeight, this);
            }

            var change = new GridOffset((float)changeGrid.TotalUnit * page, 0);

            ScrollTo(GetCurrentTGrid() + new GridOffset(change.Unit, change.Grid));
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
            else if (Keyboard.IsKeyDown(Key.LeftCtrl))
                OnWheelVerticalScale(arg);
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

        private void OnWheelVerticalScale(MouseWheelEventArgs arg)
        {
            var change = Editor.Setting.VerticalDisplayScale switch
            {
                <= 0.7 => 0.1,
                <= 1 => 0.15,
                _ => 0.3
            };
            Editor.Setting.VerticalDisplayScale = Math.Clamp(Editor.Setting.VerticalDisplayScale + Math.Sign(arg.Delta) * change, 0.1, 3);
        }

        private bool isDraggingPlayerLocation = false;
        private double draggingPlayerLocationCurrentX = 0;

        private void OnEditorUpdate(TimeSpan ts)
        {
            if (IsPreviewMode)
            {
                //record player location
                if (!isDraggingPlayerLocation)
                {
                    var tGrid = TGridCalculator.ConvertAudioTimeToTGrid(CurrentPlayTime, this);
                    var apfLane = Fumen.Lanes.GetVisibleStartObjects(tGrid, tGrid).OfType<AutoplayFaderLaneStart>()
                        .LastOrDefault();
                    var xGrid = apfLane?.CalulateXGrid(tGrid)?.TotalUnit ?? PlayerLocationRecorder.GetLocationXUnit(CurrentPlayTime);

                    PlayerLocationRecorder.Commit(CurrentPlayTime, xGrid);
                }
                else
                {
                    //user is dragging
                    var xGrid = draggingPlayerLocationCurrentX;
                    PlayerLocationRecorder.Commit(CurrentPlayTime, xGrid);
                }
            }
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

        public OngekiObjectBase? GetConflictingObject(OngekiTimelineObjectBase obj)
        {
            return (OngekiObjectBase)Fumen.GetAllDisplayableObjects().FirstOrDefault(x =>
            {
                if (x is not OngekiTimelineObjectBase tX) return false;

                // Check coordinates are the same
                if (tX.TGrid != obj.TGrid) return false;
                if (obj is OngekiMovableObjectBase movable)
                {
                    var mX = x as OngekiMovableObjectBase;
                    if (movable.XGrid != mX?.XGrid) return false;
                }

                return obj switch
                {
                    Tap => x is Tap or Hold or HoldEnd,
                    Hold => x is Hold or Tap,
                    HoldEnd => x is Tap,
                    IBulletPalleteReferencable bullet => x is IBulletPalleteReferencable bX && bX.ReferenceBulletPallete == bullet.ReferenceBulletPallete,
                    _ => obj.GetType().IsInstanceOfType(x)
                };
            });
        }

        #endregion
    }
}