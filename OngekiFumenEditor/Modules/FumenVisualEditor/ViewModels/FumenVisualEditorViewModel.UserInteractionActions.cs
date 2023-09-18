using Caliburn.Micro;
using DereTore.Common;
using Gemini.Framework;
using Gemini.Modules.Toolbox;
using Gemini.Modules.Toolbox.Models;
using NAudio.Gui;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels.DropActions;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views.UI;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using static OngekiFumenEditor.Base.OngekiObjects.Flick;

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

        public Toast Toast => (GetView() as FumenVisualEditorView)?.mainToast;

        private Dictionary<ISelectableObject, Point> currentCopiedSources = new();
        public IEnumerable<ISelectableObject> CurrentCopiedSources => currentCopiedSources.Keys;
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

        public void MenuItemAction_CopySelectedObjects()
        {
            if (IsLocked)
                return;
            if (SelectObjects.IsEmpty())
            {
                ToastNotify($"清空复制列表");
                return;
            }

            //复制所选物件
            currentCopiedSources.Clear();

            foreach (var obj in SelectObjects.Where(x => x switch
            {
                //不允许被复制
                ConnectableObjectBase and not (ConnectableStartObject) => false,
                LaneCurvePathControlObject => false,
                LaneBlockArea.LaneBlockAreaEndIndicator => false,
                Soflan.SoflanEndIndicator => false,
                //允许被复制
                _ => true,
            }))
            {
                //这里还是得再次详细过滤:
                // * Hold头可以直接被复制
                // * 轨道如果是整个轨道节点都被选中，那么它也可以被复制，否则就不准
                if (obj is ConnectableStartObject start && obj is not Hold)
                {
                    //检查start轨道节点是否全被选中了
                    if (!start.Children.OfType<ConnectableObjectBase>().Append(start).All(x => x.IsSelected))
                        continue;
                }

                var x = 0d;
                if (obj is IHorizonPositionObject horizon)
                    x = XGridCalculator.ConvertXGridToX(horizon.XGrid, this);

                var y = 0d;
                if (obj is ITimelineObject timeline)
                    y = TGridCalculator.ConvertTGridToY_DesignMode(timeline.TGrid, this);

                var canvasPos = new Point(x, y);

                //注册,并记录当前位置
                currentCopiedSources[obj] = canvasPos;
            }

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
        {
            var placePos = Mouse.GetPosition(GetView() as FrameworkElement);
            placePos.Y = ViewHeight - placePos.Y + Rect.MinY;
            PasteCopiesObjects(PasteMirrorOption.None, placePos);
        }
        public void MenuItemAction_PasteCopiesObjects(ActionExecutionContext ctx)
            => PasteCopiesObjects(PasteMirrorOption.None, ctx);
        public void MenuItemAction_PasteCopiesObjectsAsSelectedRangeCenterXGridMirror(ActionExecutionContext ctx)
            => PasteCopiesObjects(PasteMirrorOption.SelectedRangeCenterXGridMirror, ctx);
        public void MenuItemAction_PasteCopiesObjectsAsSelectedRangeCenterTGridMirror(ActionExecutionContext ctx)
            => PasteCopiesObjects(PasteMirrorOption.SelectedRangeCenterTGridMirror, ctx);
        public void MenuItemAction_PasteCopiesObjectsAsXGridZeroMirror(ActionExecutionContext ctx)
            => PasteCopiesObjects(PasteMirrorOption.XGridZeroMirror, ctx);

        public void PasteCopiesObjects(PasteMirrorOption mirrorOption, ActionExecutionContext ctx)
        {
            var placePos = contextMenuPosition;
            placePos.Y = ViewHeight - placePos.Y + Rect.MinY;
            PasteCopiesObjects(mirrorOption, placePos);
        }

        public void PasteCopiesObjects(PasteMirrorOption mirrorOption, Point? placePoint = default)
        {
            if (IsLocked)
                return;

            //先取消选择所有的物件
            TryCancelAllObjectSelecting();

            Point CalculateRangeCenter(IEnumerable<ISelectableObject> objects)
            {
                (var minX, var maxX) = objects
                        .Select(x => currentCopiedSources.TryGetValue(x, out var p) ? (true, p.X) : default)
                        .Where(x => x.Item1)
                        .Select(x => x.X)
                        .MaxMinBy(x => x);

                var diffX = maxX - minX;
                var x = minX + diffX / 2f;

                (var minY, var maxY) = objects
                        .Select(x => currentCopiedSources.TryGetValue(x, out var p) ? (true, p.Y) : default)
                        .Where(x => x.Item1)
                        .Select(x => x.Y)
                        .MaxMinBy(x => x);

                var diffY = maxY - minY;
                var y = minY + diffY / 2f;

                return new(x, y);
            }

            double? CalculateXMirror(IEnumerable<ISelectableObject> objects, PasteMirrorOption mirrorOption)
            {
                if (mirrorOption == PasteMirrorOption.XGridZeroMirror)
                    return XGridCalculator.ConvertXGridToX(0, this);

                if (mirrorOption == PasteMirrorOption.SelectedRangeCenterXGridMirror)
                {
                    (var minX, var maxX) = objects
                        .Select(x => currentCopiedSources.TryGetValue(x, out var p) ? (true, p.X) : default)
                        .Where(x => x.Item1)
                        .Select(x => x.X)
                        .MaxMinBy(x => x);

                    var diffX = maxX - minX;
                    var mirrorX = minX + diffX / 2f;
                    return mirrorX;
                }

                return null;
            }

            double? CalculateYMirror(IEnumerable<ISelectableObject> objects, PasteMirrorOption mirrorOption)
            {
                if (mirrorOption != PasteMirrorOption.SelectedRangeCenterTGridMirror)
                    return null;

                (var minY, var maxY) = objects
                        .Select(x => currentCopiedSources.TryGetValue(x, out var p) ? (true, p.Y) : default)
                        .Where(x => x.Item1)
                        .Select(x => x.Y)
                        .MaxMinBy(x => x);

                var diffY = maxY - minY;
                var mirrorY = minY + diffY / 2f;
                return mirrorY;
            }

            //计算出镜像中心位置
            var mirrorYOpt = CalculateYMirror(currentCopiedSources.Keys, mirrorOption);
            var mirrorXOpt = CalculateXMirror(currentCopiedSources.Keys, mirrorOption);

            var sourceCenterPos = CalculateRangeCenter(currentCopiedSources.Keys);
            var offset = (placePoint ?? sourceCenterPos) - sourceCenterPos;

            if (mirrorOption == PasteMirrorOption.XGridZeroMirror)
                offset.X = 0;

            var redo = new System.Action(() => { });
            var undo = new System.Action(() => { });

            foreach (var pair in currentCopiedSources)
            {
                var source = pair.Key as OngekiObjectBase;
                var sourceCanvasPos = pair.Value;

                var copied = source.CopyNew();
                if (copied is null)
                    continue;

                switch (copied)
                {
                    //特殊处理ConnectableStart:连Child和Control一起复制了,顺便删除RecordId(添加时需要重新分配而已)
                    case ConnectableStartObject _start:
                        _start.CopyEntireConnectableObject((ConnectableStartObject)source);
                        redo += () => _start.RecordId = -1;
                        break;
                    //特殊处理LBK:连End物件一起复制了
                    case LaneBlockArea _lbk:
                        _lbk.CopyEntire((LaneBlockArea)source);
                        break;
                    //特殊处理SFL:连End物件一起复制了
                    case Soflan _sfl:
                        _sfl.CopyEntire((Soflan)source);
                        break;
                    //特殊处理Hold:清除Id
                    case Hold hold:
                        hold.ReferenceLaneStart = default;
                        undo += () => hold.ReferenceLaneStart = default;
                        break;
                    case Flick flick:
                        if (mirrorOption == PasteMirrorOption.XGridZeroMirror
                            || mirrorOption == PasteMirrorOption.SelectedRangeCenterXGridMirror)
                        {
                            var beforeDirection = flick.Direction;
                            redo += () => flick.Direction = (FlickDirection)(-(int)beforeDirection);
                            undo += () => flick.Direction = beforeDirection;
                        }
                        break;
                    default:
                        break;
                }

                TGrid newTGrid = default;
                if (copied is ITimelineObject timelineObject)
                {
                    var tGrid = timelineObject.TGrid.CopyNew();

                    double CalcY(double y)
                    {
                        var mirrorBaseY = mirrorYOpt is double _mirrorY ? _mirrorY : y;
                        var mirroredY = mirrorBaseY + mirrorBaseY - y;
                        var offsetedY = mirroredY + offset.Y;

                        return offsetedY;
                    }

                    var newY = CalcY(sourceCanvasPos.Y);

                    if (TGridCalculator.ConvertYToTGrid_DesignMode(newY, this) is not TGrid nt)
                    {
                        //todo warn
                        return;
                    }

                    newTGrid = nt;
                    redo += () => timelineObject.TGrid = newTGrid.CopyNew();
                    undo += () => timelineObject.TGrid = tGrid.CopyNew();

                    switch (copied)
                    {
                        case Soflan or LaneBlockArea:
                            ITimelineObject endIndicator = copied switch
                            {
                                Soflan _sfl => _sfl.EndIndicator,
                                LaneBlockArea _lbk => _lbk.EndIndicator,
                                _ => throw new Exception("这都能炸真的牛皮")
                            };
                            var oldEndIndicatorTGrid = endIndicator.TGrid.CopyNew();
                            var endIndicatorY = TGridCalculator.ConvertTGridToY_DesignMode(oldEndIndicatorTGrid, this);
                            var newEndIndicatorY = CalcY(endIndicatorY);

                            if (TGridCalculator.ConvertYToTGrid_DesignMode(newEndIndicatorY, this) is not TGrid newEndIndicatorTGrid)
                            {
                                //todo warn
                                return;
                            }

                            redo += () => endIndicator.TGrid = newEndIndicatorTGrid.CopyNew();
                            undo += () => endIndicator.TGrid = oldEndIndicatorTGrid.CopyNew();

                            break;
                        case ConnectableStartObject start:
                            //apply child objects
                            foreach (var child in start.Children)
                            {
                                var oldChildTGrid = child.TGrid.CopyNew();
                                var y = TGridCalculator.ConvertTGridToY_DesignMode(oldChildTGrid, this);
                                var newChildY = CalcY(y);

                                if (TGridCalculator.ConvertYToTGrid_DesignMode(newChildY, this) is not TGrid newChildTGrid)
                                {
                                    //todo warn
                                    return;
                                }

                                redo += () => child.TGrid = newChildTGrid.CopyNew();
                                undo += () => child.TGrid = oldChildTGrid.CopyNew();

                                foreach (var control in child.PathControls)
                                {
                                    var oldControlTGrid = control.TGrid.CopyNew();
                                    var cy = TGridCalculator.ConvertTGridToY_DesignMode(oldControlTGrid, this);
                                    var newControlY = CalcY(cy);

                                    if (TGridCalculator.ConvertYToTGrid_DesignMode(newControlY, this) is not TGrid newControlTGrid)
                                    {
                                        //todo warn
                                        return;
                                    }

                                    redo += () => control.TGrid = newControlTGrid.CopyNew();
                                    undo += () => control.TGrid = oldControlTGrid.CopyNew();
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }

                XGrid newXGrid = default;
                var offsetedX = 0d; //后面会用到,因此提出来
                if (copied is IHorizonPositionObject horizonPositionObject)
                {
                    var xGrid = horizonPositionObject.XGrid.CopyNew();

                    double CalcX(double x)
                    {
                        var mirrorBaseX = mirrorXOpt is double _mirrorX ? _mirrorX : x;
                        var mirroredX = mirrorBaseX + mirrorBaseX - x;
                        offsetedX = mirroredX + offset.X;

                        return offsetedX;
                    }

                    var newX = CalcX(sourceCanvasPos.X);

                    if (XGridCalculator.ConvertXToXGrid(offsetedX, this) is not XGrid nx)
                    {
                        //todo warn
                        return;
                    }

                    newXGrid = nx;
                    undo += () => horizonPositionObject.XGrid = xGrid.CopyNew();
                    redo += () => horizonPositionObject.XGrid = newXGrid.CopyNew();

                    //apply child objects
                    if (copied is ConnectableStartObject start)
                    {
                        foreach (var child in start.Children)
                        {
                            var oldChildXGrid = child.XGrid.CopyNew();
                            var x = XGridCalculator.ConvertXGridToX(oldChildXGrid, this);
                            var newChildX = CalcX(x);

                            if (XGridCalculator.ConvertXToXGrid(newChildX, this) is not XGrid newChildXGrid)
                            {
                                //todo warn
                                return;
                            }

                            redo += () => child.XGrid = newChildXGrid.CopyNew();
                            undo += () => child.XGrid = oldChildXGrid.CopyNew();

                            foreach (var control in child.PathControls)
                            {
                                var oldControlXGrid = control.XGrid.CopyNew();
                                var cx = XGridCalculator.ConvertXGridToX(oldControlXGrid, this);
                                var newControlX = CalcX(cx);


                                if (XGridCalculator.ConvertXToXGrid(newControlX, this) is not XGrid newControlXGrid)
                                {
                                    //todo warn
                                    return;
                                }

                                redo += () => control.XGrid = newControlXGrid.CopyNew();
                                undo += () => control.XGrid = oldControlXGrid.CopyNew();
                            }
                        }
                    }
                }

                if (copied is ILaneDockable dockable)
                {
                    var before = dockable.ReferenceLaneStart;

                    redo += () =>
                    {
                        //这里做个检查吧:如果复制新的位置刚好也(靠近)在原来附着的轨道上，那就不变，否则就得清除ReferenceLaneStart
                        //todo 后面可以做更细节的检查和变动
                        if (dockable.ReferenceLaneStart is LaneStartBase beforeStart)
                        {
                            var needRedockLane = true;
                            if (beforeStart.CalulateXGrid(newTGrid) is XGrid xGrid)
                            {
                                var x = XGridCalculator.ConvertXGridToX(xGrid, this);
                                var diff = offsetedX - x;

                                if (Math.Abs(diff) < 8)
                                {
                                    //那就是在轨道上，不用动了！
                                    needRedockLane = false;
                                }
                                else
                                {
                                    dockable.ReferenceLaneStart = default;
                                }
                            }

                            if (needRedockLane)
                            {
                                var dockableLanes = Fumen.Lanes
                                    .GetVisibleStartObjects(newTGrid, newTGrid)
                                    .Where(x => x.IsDockableLane && x != beforeStart)
                                    .OrderBy(x => Math.Abs(x.LaneType - beforeStart.LaneType));

                                var pickLane = dockableLanes.FirstOrDefault();

                                //不在轨道上，那就清除惹
                                //todo 重新钦定一个轨道
                                dockable.ReferenceLaneStart = pickLane;
                            }
                        }
                        else
                            dockable.ReferenceLaneStart = default;
                    };

                    undo += () => dockable.ReferenceLaneStart = before;
                }

                var map = new Dictionary<ISelectableObject, bool>();
                foreach (var selectObj in ((copied as IDisplayableObject)?.GetDisplayableObjects() ?? Enumerable.Empty<IDisplayableObject>()).OfType<ISelectableObject>())
                    map[selectObj] = selectObj.IsSelected;

                redo += () =>
                {
                    Fumen.AddObject(copied);
                    foreach (var selectObj in map.Keys)
                        selectObj.IsSelected = true;
                };

                undo += () =>
                {
                    RemoveObject(copied);
                    foreach (var pair in map)
                        pair.Key.IsSelected = pair.Value;
                };
            };

            redo += () => IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(this);
            undo += () => IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(this);

            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("复制粘贴", redo, undo));
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
                ToastNotify("请先将编辑器切换到设计模式");
                return;
            }

            cacheObjectAudioTime.Clear();
            foreach (var obj in SelectObjects)
            {
                if (obj is ITimelineObject timelineObject)
                    cacheObjectAudioTime[timelineObject] = TGridCalculator.ConvertTGridToY_DesignMode(timelineObject.TGrid, this);
                else
                    ToastNotify($"无法记忆此物件，因为此物件没有实现ITimelineObject : {obj}");
            }

            ToastNotify($"已记忆 {cacheObjectAudioTime.Count} 个物件的音频时间");
        }

        public void MenuItemAction_RecoverySelectedObjectToAudioTime()
        {
            if (!IsDesignMode)
            {
                ToastNotify("请先将编辑器切换到设计模式");
                return;
            }

            var recoverTargets = Fumen.GetAllDisplayableObjects()
                .OfType<ITimelineObject>()
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
                        timelineObject.TGrid = TGridCalculator.ConvertYToTGrid_DesignMode(audioTime, this);

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

        private void SelectRangeObjects()
        {
            if (!IsDesignMode)
            {
                ToastNotify("请先将编辑器切换到设计模式");
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

        #region Keyboard Actions

        public void KeyboardAction_DeleteSelectingObjects()
        {
            if (IsLocked)
                return;

            //删除已选择的物件
            var selectedObject = SelectObjects.OfType<OngekiObjectBase>().ToArray();

            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("删除物件", () =>
            {
                foreach (var obj in selectedObject)
                    RemoveObject(obj);
            }, () =>
            {
                foreach (var obj in selectedObject)
                    Fumen.AddObject(obj);
            }));
        }

        public void RemoveObject(OngekiObjectBase obj)
        {
            if (obj is ISelectableObject selectable)
                selectable.IsSelected = false;
            Fumen.RemoveObject(obj);

            var propertyBrowser = IoC.Get<IFumenObjectPropertyBrowser>();
            if (IsActive)
                propertyBrowser.RefreshSelected(this);
        }

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

        public void KeyboardAction_FastSetObjectIsCritical()
        {
            var selectables = SelectObjects.ToArray();
            var map = new Dictionary<ICriticalableObject, bool>();

            var isAllCritical = true;
            foreach (var selectable in selectables.OfType<ICriticalableObject>())
                isAllCritical &= map[selectable] = selectable.IsCritical;

            if (map.Count == 0)
            {
                ToastNotify("无合适物件批量设置IsCritical属性");
                return;
            }

            var setVal = !isAllCritical;

            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("快速批量设置IsCritical", () =>
            {
                foreach (var pair in map)
                    pair.Key.IsCritical = setVal;
            }, () =>
            {
                foreach (var pair in map)
                    pair.Key.IsCritical = pair.Value;
            }));
        }

        public void KeyboardAction_FastAddConnectableChild(ActionExecutionContext e)
        {
            if (!IsDesignMode)
                return;
            var propertyBrowser = IoC.Get<IFumenObjectPropertyBrowser>();
            if (!(propertyBrowser.SelectedObjects.Count == 1 && propertyBrowser.SelectedObjects.FirstOrDefault() is ConnectableObjectBase connectable))
                return;

            var startObj = connectable switch
            {
                ConnectableStartObject start => start,
                ConnectableNextObject next => next.ReferenceStartObject,
                _ => default
            };

            if (startObj is null || startObj.Children.OfType<ConnectableEndObject>().Any())
                return;

            var genChild = startObj.CreateNextObject();
            var position = Mouse.GetPosition(e.View as FrameworkElement);

            position.Y = ViewHeight - position.Y + Rect.MinY;

            var dropAction = new ConnectableObjectDropAction(startObj, genChild, () => { });
            dropAction.Drop(this, position);
        }

        public void KeyboardAction_PlayOrPause()
        {
            IoC.Get<IAudioPlayerToolViewer>().RequestPlayOrPause();
        }

        public void KeyboardAction_HideOrShow()
        {
            SwitchMode(!IsPreviewMode);
        }

        private void SwitchMode(bool isPreviewMode)
        {
            var tGrid = GetCurrentTGrid();
            IsUserRequestHideEditorObject = isPreviewMode;
            convertToY = IsDesignMode ?
                TGridCalculator.ConvertTGridUnitToY_DesignMode :
                TGridCalculator.ConvertTGridUnitToY_PreviewMode;
            RecalculateTotalDurationHeight();
            ScrollTo(tGrid);
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
                            var compositeAction = UndoRedoManager.EndCombineAction("物件拖动");
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
                        ScrollViewerVerticalOffset = Math.Max(0, Math.Min(TotalDurationHeight, startScrollOffset + diffY));
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
                position.Y = Math.Min(TotalDurationHeight, Math.Max(0, Rect.MaxY - position.Y));

                isLeftMouseDown = true;
                isSelectRangeDragging = false;

                var hitResult = hits.AsParallel().Where(x => x.Value.Contains(position)).Select(x => x.Key).OrderBy(x => x.Id).ToArray();
                var idx = Math.Max(0, hitResult.IndexOf(mouseDownHitObject));
                var hitOngekiObject = hitResult.ElementAtOrDefault(idx);

                Log.LogDebug($"mousePos = （{position.X:F0},{position.Y:F0}) , hitOngekiObject = {hitOngekiObject}");

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

                    if (hitResult.Length > 1)
                    {
                        var nextIdx = (idx + 1) % hitResult.Length;
                        mouseDownNextHitObject = hitResult[nextIdx];
                    }
                }
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
                ScrollViewerVerticalOffset = Math.Max(0, Math.Min(TotalDurationHeight, startScrollOffset + diffY));

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
                    ScrollTo(y);

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
            if (!(CurrentCopiedSources.IsOnlyOne(out var c) && c is OngekiObjectBase copySouceObj))
                return;

            var newObject = copySouceObj.CopyNew();
            if (newObject is null
                //不支持笔刷模式下新建以下玩意
                || newObject is ConnectableStartObject
                || newObject is ConnectableEndObject)
            {
                ToastNotify($"笔刷模式下不支持{copySouceObj?.Name}");
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

            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("刷子物件添加", redo, undo));
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

            var tGrid = IsDesignMode ?
                    TGridCalculator.ConvertYToTGrid_DesignMode(canvasY, this) :
                    TGridCalculator.ConvertYToTGrid_PreviewMode(canvasY, this);
            TimeSpan? audioTime = tGrid is not null ? TGridCalculator.ConvertTGridToAudioTime(tGrid, this) : null;
            var xGrid = XGridCalculator.ConvertXToXGrid(canvasX, this);
            contentObject.Message = $"C[{canvasX:F2},{canvasY:F2}] {(tGrid is not null ? $"T[{tGrid.Unit},{tGrid.Grid}]" : "T[N/A]")} X[{xGrid.Unit:F2},{xGrid.Grid}] A[{audioTime?.ToString("mm\\:ss\\.fff")}]";
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

        private Dictionary<OngekiObjectBase, Rect> hits = new();
        private OngekiObjectBase mouseDownNextHitObject;
        private Point mouseCanvasStartPosition;
        private double startXOffset;
        private double startScrollOffset;
        private bool isCanvasDragging;
        private bool isMiddleMouseDown;
        private MouseButtonState prevRightButtonState;
        private Point contextMenuPosition;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RegisterSelectableObject(OngekiObjectBase obj, Vector2 centerPos, Vector2 size)
        {
            //rect.Y = rect.Y - CurrentPlayTime;
            hits[obj] = new Rect(centerPos.X - size.X / 2, centerPos.Y - size.Y / 2, size.X, size.Y);
        }

        public void OnMouseWheel(ActionExecutionContext e)
        {
            if (IsLocked)
                return;

            var arg = e.EventArgs as MouseWheelEventArgs;
            arg.Handled = true;

            if (Setting.JudgeLineAlignBeat && IsDesignMode)
            {
                var tGrid = GetCurrentTGrid();
                var time = TGridCalculator.ConvertTGridToAudioTime(tGrid, this);
                var y = TGridCalculator.ConvertTGridToY_DesignMode(tGrid, this);

                var timeSignatures = Fumen.MeterChanges.GetCachedAllTimeSignatureUniformPositionList(Setting.TGridUnitLength, Fumen.BpmList);
                (var prevAudioTime, _, var meter, var bpm) = timeSignatures.LastOrDefault(x => x.audioTime < time);
                if (meter is null)
                    (prevAudioTime, _, meter, bpm) = timeSignatures.FirstOrDefault();

                var nextY = ScrollViewerVerticalOffset + TGridCalculator.CalculateOffsetYPerBeat(bpm, meter, Setting.BeatSplit, Setting.VerticalDisplayScale, Setting.TGridUnitLength) * 2;
                //消除精度误差~
                var prevY = Math.Max(0, TGridCalculator.ConvertAudioTimeToY_DesignMode(prevAudioTime, this) - 1);

                var downs = TGridCalculator.GetVisbleTimelines_DesignMode(Fumen.Soflans, Fumen.BpmList, Fumen.MeterChanges, prevY, ScrollViewerVerticalOffset, 0, Setting.BeatSplit, Setting.VerticalDisplayScale, Setting.TGridUnitLength);
                var downFirst = downs.Where(x => x.tGrid != tGrid).LastOrDefault();
                var nexts = TGridCalculator.GetVisbleTimelines_DesignMode(Fumen.Soflans, Fumen.BpmList, Fumen.MeterChanges, ScrollViewerVerticalOffset, nextY, 0, Setting.BeatSplit, Setting.VerticalDisplayScale, Setting.TGridUnitLength);
                var nextFirst = nexts.Where(x => x.tGrid != tGrid).FirstOrDefault();

                var result = arg.Delta > 0 ? nextFirst : downFirst;
                if (result.tGrid is not null)
                    ScrollTo(result.y);
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
                    ScrollTo(ScrollViewerVerticalOffset + Math.Sign(arg.Delta) * Setting.MouseWheelLength);
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
            Toast?.ShowMessage(message);
            Log.LogInfo(message);
        }

        #region Object Interaction

        public void MoveObjectTo(OngekiObjectBase obj, Point point) => InteractiveManager.GetInteractive(obj).OnMoveCanvas(obj, point, this);

        #endregion
    }
}
