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
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views.UI;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static OngekiFumenEditor.Base.OngekiObjects.BulletPallete;

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

        private HashSet<ISelectableObject> currentCopiedSources = new();
        public IEnumerable<ISelectableObject> CurrentCopiedSources => currentCopiedSources;
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

            IsPreventMutualExclusionSelecting = false;
        }

        public void MenuItemAction_ReverseSelect()
        {
            IsPreventMutualExclusionSelecting = true;

            Fumen.GetAllDisplayableObjects().OfType<ISelectableObject>().ForEach(x => x.IsSelected = !x.IsSelected);

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
            var newObjects = currentCopiedSources.OfType<OngekiObjectBase>().Select(x => x.CopyNew(Fumen)).FilterNull().ToList();
            var mirrorTGrid = CalculateTGridMirror(newObjects, mirrorOption);
            var mirrorXGrid = CalculateXGridMirror(newObjects, mirrorOption);

            var redo = new System.Action(() => { });
            var undo = new System.Action(() => { });

            var idMap = new Dictionary<int, int>();

            var partOfConnectableObjects = newObjects
                .Select(x => x)
                .OfType<ConnectableObjectBase>()
                .GroupBy(x => x.RecordId);

            foreach (var lane in partOfConnectableObjects.Where(x => !x.OfType<ConnectableStartObject>().Any()).ToArray())
            {
                if (lane.IsOnlyOne(out var headChildObject))
                {
                    //同id组里面只有单个子节点，那就不给它单独转换和复制粘贴了
                    newObjects.RemoveAll(x => x == headChildObject);
                    Log.LogDebug($"detect only one child in same recordId ,remove it. headChildObject : {headChildObject}");
                    continue;
                }

                var refRecordId = -headChildObject.RecordId;
                var refSourceHeadChildObject = currentCopiedSources.Select(x => x).OfType<ConnectableChildObjectBase>().FirstOrDefault(x => x.RecordId == refRecordId);

                var newStartObject = LambdaActivator.CreateInstance(refSourceHeadChildObject.ReferenceStartObject.GetType()) as OngekiObjectBase;

                newStartObject.Copy(headChildObject, Fumen);

                newObjects.RemoveAll(x => x == headChildObject);
                newObjects.Insert(0, newStartObject);

                Log.LogDebug($"detect non-include start object copying , remove head of children and add new start object, headChildObject : {headChildObject}");
            }

            foreach (var displayObjectView in newObjects)
            {
                if (displayObjectView is ITimelineObject timelineObject)
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

                if (displayObjectView is IHorizonPositionObject horizonPositionObject)
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

                var selectObj = displayObjectView as ISelectableObject;
                var isSelect = selectObj.IsSelected;

                switch (displayObjectView)
                {
                    case ConnectableStartObject startObject:
                        var rawId = startObject.RecordId;
                        redo += () =>
                        {
                            Fumen.AddObject(displayObjectView);
                            var newId = startObject.RecordId;
                            idMap[rawId] = newId;
                            selectObj.IsSelected = true;
                        };

                        undo += () =>
                        {
                            RemoveObject(displayObjectView);
                            startObject.RecordId = rawId;
                            selectObj.IsSelected = isSelect;
                        };
                        break;
                    case ConnectableChildObjectBase childObject:
                        var rawChildId = childObject.RecordId;
                        redo += () =>
                        {
                            if (idMap.TryGetValue(rawChildId, out var newChildId))
                                childObject.RecordId = newChildId;
                            Fumen.AddObject(displayObjectView);
                            selectObj.IsSelected = true;
                        };

                        undo += () =>
                        {
                            RemoveObject(displayObjectView);
                            childObject.RecordId = rawChildId;
                            selectObj.IsSelected = isSelect;
                        };
                        break;
                    default:
                        redo += () =>
                        {
                            Fumen.AddObject(displayObjectView);
                            selectObj.IsSelected = true;
                        };

                        undo += () =>
                        {
                            RemoveObject(displayObjectView);
                            selectObj.IsSelected = isSelect;
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

        private XGrid CalculateXGridMirror(IEnumerable<OngekiObjectBase> newObjects, PasteMirrorOption mirrorOption)
        {
            if (mirrorOption == PasteMirrorOption.XGridZeroMirror)
                return XGrid.Zero;

            if (mirrorOption == PasteMirrorOption.SelectedRangeCenterXGridMirror)
            {
                (var min, var max) = newObjects
                .Select(x => x as IHorizonPositionObject)
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

        private TGrid CalculateTGridMirror(IEnumerable<OngekiObjectBase> newObjects, PasteMirrorOption mirrorOption)
        {
            if (mirrorOption != PasteMirrorOption.SelectedRangeCenterTGridMirror)
                return default;

            (var min, var max) = newObjects
                .Select(x => x as ITimelineObject)
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
        private OngekiObjectBase mouseDownHitObject;
        private Point? mouseDownHitObjectPosition;
        private Point mouseStartPosition;
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
                foreach (var o in selectObjects.OfType<ISelectableObject>())
                    o.IsSelected = true;
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
            var selectedObject = SelectObjects.OfType<OngekiObjectBase>().ToArray();

            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("删除物件", () =>
            {
                foreach (var obj in selectedObject)
                    RemoveObject(obj);

                Redraw(RedrawTarget.OngekiObjects);
            }, () =>
            {
                foreach (var obj in selectedObject)
                {
                    Fumen.AddObject(obj);
                }

                Redraw(RedrawTarget.OngekiObjects);
            }));
        }

        public void RemoveObject(OngekiObjectBase obj)
        {
            if (obj is ISelectableObject selectable)
                selectable.IsSelected = false;
            Fumen.RemoveObject(obj);

            var propertyBrowser = IoC.Get<IFumenObjectPropertyBrowser>();
            if (propertyBrowser != null && propertyBrowser.OngekiObject == obj)
                propertyBrowser.SetCurrentOngekiObject(default, this);
        }

        public void KeyboardAction_SelectAllObjects()
        {
            if (IsLocked)
                return;

            Fumen.GetAllDisplayableObjects().OfType<ISelectableObject>().ForEach(x => x.IsSelected = true);
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
            SwitchMode(!IsPreviewMode);
        }

        private void SwitchMode(bool isPreviewMode)
        {
            var tGrid = GetCurrentTGrid();
            IsUserRequestHideEditorObject = isPreviewMode;
            convertToY = IsDesignMode ?
                TGridCalculator.ConvertTGridUnitToY_DesignMode :
                TGridCalculator.ConvertTGridUnitToY_PreviewMode;
            RecalculateScrollBar();
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
            if (IsLocked)
                return;

            if (!(isMouseDown && (e.View as FrameworkElement)?.Parent is IInputElement parent))
                return;

            if (IsDesignMode)
            {
                if (IsRangeSelecting && SelectionCurrentCursorPosition != SelectionStartPosition)
                {
                    SelectRangeObjects();
                }
                else
                {
                    var pos = (e.EventArgs as MouseEventArgs).GetPosition(parent);
                    if (isDragging)
                    {
                        var cp = pos;
                        cp.Y = ViewHeight - cp.Y + Rect.MinY;
                        SelectObjects.ToArray().ForEach(x =>
                        {
                            var obj = x as OngekiObjectBase;
                            InteractiveManager.GetInteractive(obj).OnDragEnd(obj, cp, this);
                        });
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
                                NotifyObjectClicked(mouseDownHitObject);
                        }
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
            if (!(CurrentCopiedSources.IsOnlyOne(out var c) && c is OngekiObjectBase copySouceObj))
                return;

            var newObject = copySouceObj.CopyNew(Fumen);
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
                Redraw(RedrawTarget.OngekiObjects);
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
                    Redraw(RedrawTarget.OngekiObjects);
                }
            };

            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("刷子物件添加", redo, undo));
        }

        public void OnMouseDown(ActionExecutionContext e)
        {
            if (IsLocked)
                return;

            var view = e.View as FrameworkElement;

            if ((e.EventArgs as MouseEventArgs).LeftButton == MouseButtonState.Pressed)
            {
                isMouseDown = true;
                isDragging = false;

                var arg = e.EventArgs as MouseEventArgs;
                var position = arg.GetPosition(e.Source);
                position.Y = Math.Min(TotalDurationHeight, Math.Max(0, Rect.MaxY - position.Y));
                var hitResult = Enumerable.Empty<KeyValuePair<OngekiObjectBase, Rect>>();

                hitResult = hits.AsParallel().Where(x => x.Value.Contains(position)).ToArray();
                var hitOngekiObject = hitResult.FirstOrDefault().Key;

                Log.LogDebug($"mousePos = （{position.X:F0},{position.Y:F0}) , hitOngekiObject = {hitOngekiObject}");

                mouseDownHitObject = null;
                mouseDownHitObjectPosition = default;
                mouseStartPosition = position;
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
                    SelectionVisibility = Visibility.Collapsed;
                    mouseDownHitObject = hitOngekiObject;
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

            if (!IsDesignMode)
                return;

            var r = isDragging;
            isDragging = true;
            var dragCall = new Action<OngekiObjectBase, Point>((vm, pos) =>
            {
                var action = InteractiveManager.GetInteractive(vm);
                if (r)
                    action.OnDragMove(vm, pos, this);
                else
                    action.OnDragStart(vm, pos, this);
            });

            var rp = 1 - pos.Y / ViewHeight;
            var srp = 1 - mouseStartPosition.Y / ViewHeight;
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

        #region Object Click&Selection

        public void TryCancelAllObjectSelecting(params ISelectableObject[] expects)
        {
            var objBrowser = IoC.Get<IFumenObjectPropertyBrowser>();
            var curBrowserObj = objBrowser.OngekiObject;
            expects = expects ?? new ISelectableObject[0];

            if (!(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || IsRangeSelecting || IsPreventMutualExclusionSelecting))
            {
                foreach (var o in SelectObjects.Where(x => !expects.Contains(x)))
                {
                    o.IsSelected = false;
                    if (o == curBrowserObj)
                        objBrowser.SetCurrentOngekiObject(null, this);
                }
            }
        }

        public void NotifyObjectClicked(OngekiObjectBase obj)
        {
            if (obj is not ISelectableObject selectable)
                return;

            var objBrowser = IoC.Get<IFumenObjectPropertyBrowser>();
            var curBrowserObj = objBrowser.OngekiObject;
            var count = SelectObjects.Take(2).Count();
            var first = SelectObjects.FirstOrDefault();

            if ((count > 1) || (count == 1 && first != obj)) //比如你目前有多个已选择的，但你单点了一个
            {
                TryCancelAllObjectSelecting(obj as ISelectableObject);
                selectable.IsSelected = true;
                objBrowser.SetCurrentOngekiObject(obj, this);
            }
            else
            {
                selectable.IsSelected = !selectable.IsSelected;
                if (selectable.IsSelected)
                    objBrowser.SetCurrentOngekiObject(obj, this);
                else if (obj == curBrowserObj)
                    objBrowser.SetCurrentOngekiObject(null, this);
                TryCancelAllObjectSelecting(obj as ISelectableObject);
            }
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
            if (IsDesignMode)
            {
                var tGrid = TGridCalculator.ConvertYToTGrid_DesignMode(canvasY, this);
                TimeSpan? audioTime = tGrid is not null ? TGridCalculator.ConvertTGridToAudioTime(tGrid, this) : null;
                var xGrid = XGridCalculator.ConvertXToXGrid(canvasX, this);
                contentObject.Message = $"C[{canvasX:F2},{canvasY:F2}] {(tGrid is not null ? $"T[{tGrid.Unit},{tGrid.Grid}]" : "T[N/A]")} X[{xGrid.Unit:F2},{xGrid.Grid}] A[{audioTime?.ToString("mm\\:ss\\.fff")}]";
            }
            else
            {
                contentObject.Message = string.Empty;
            }
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
            if (!arg.Data.GetDataPresent(ToolboxDragDrop.DataFormat))
                return;

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

        #endregion

        private Dictionary<OngekiObjectBase, Rect> hits = new();

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
            if (!IsDesignMode)
                return;

            var arg = e.EventArgs as MouseWheelEventArgs;
            arg.Handled = true;

            if (Setting.JudgeLineAlignBeat)
            {
                var time = TimeSpan.FromMilliseconds(ScrollViewerVerticalOffset);

                var timeSignatures = Fumen.MeterChanges.GetCachedAllTimeSignatureUniformPositionList(Setting.TGridUnitLength, Fumen.BpmList);
                (var prevAudioTime, _, var meter, var bpm) = timeSignatures.LastOrDefault(x => x.audioTime < time);
                if (meter is null)
                    (prevAudioTime, _, meter, bpm) = timeSignatures.FirstOrDefault();

                var nextY = ScrollViewerVerticalOffset + TGridCalculator.CalculateOffsetYPerBeat(bpm, meter, Setting.BeatSplit, Setting.VerticalDisplayScale, Setting.TGridUnitLength);
                //消除精度误差~
                var prevY = Math.Max(0, prevAudioTime.TotalMilliseconds - 1);
                nextY++;

                var downFirst = TGridCalculator.GetVisbleTimelines_DesignMode(Fumen.Soflans, Fumen.BpmList, Fumen.MeterChanges, prevY, ScrollViewerVerticalOffset, 0, Setting.BeatSplit, Setting.VerticalDisplayScale, Setting.TGridUnitLength)
                    .Where(x => x.y != ScrollViewerVerticalOffset).LastOrDefault();
                var nextFirst = TGridCalculator.GetVisbleTimelines_DesignMode(Fumen.Soflans, Fumen.BpmList, Fumen.MeterChanges, ScrollViewerVerticalOffset, nextY, 0, Setting.BeatSplit, Setting.VerticalDisplayScale, Setting.TGridUnitLength)
                    .Where(x => x.y != ScrollViewerVerticalOffset).FirstOrDefault();

                //Log.LogDebug($"ScrollViewerVerticalOffset: {ScrollViewerVerticalOffset:F2} , downFirst: {downFirst.y:F2} , nextFirst: {nextFirst.y:F2}");
                var result = arg.Delta > 0 ? nextFirst : downFirst;
                ScrollTo(result.y);
            }
            else
            {
                ScrollTo(ScrollViewerVerticalOffset + Math.Sign(arg.Delta) * Setting.MouseWheelLength);
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

        #region Object Interaction

        public void MoveObjectTo(OngekiObjectBase obj, Point point) => InteractiveManager.GetInteractive(obj).OnMoveCanvas(obj, point, this);

        #endregion
    }
}
