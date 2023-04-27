using Caliburn.Micro;
using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Kernel.CurveInterpolater.DefaultImpl.Factory;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels.Dialog;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels.DropActions;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.Views;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels
{
    [MapToView(ViewType = typeof(ConnectableObjectOperationView))]
    public abstract class ConnectableObjectOperationViewModel : PropertyChangedBase
    {
        public enum DragActionType
        {
            DropEnd,
            DropNext,
            DropCurvePathControl,
            Split
        }

        private bool _draggingItem;
        private Point _mouseStartPosition;

        private ConnectableObjectBase connectableObject;
        public ConnectableObjectBase ConnectableObject
        {
            get
            {
                return connectableObject;
            }
            set
            {
                connectableObject = value;
                NotifyOfPropertyChange(() => ConnectableObject);
                CheckEnable();
            }
        }

        public ConnectableStartObject RefStartObject => ConnectableObject switch
        {
            ConnectableStartObject start => start,
            ConnectableChildObjectBase next => next.ReferenceStartObject,
            _ => default,
        };

        public bool IsEnableDragEnd => !(RefStartObject?.Children.OfType<ConnectableEndObject>().Any() ?? false);
        public bool IsEnableDragPathControl => ConnectableObject is ConnectableChildObjectBase;
        public bool IsStartObject => ConnectableObject is ConnectableStartObject;

        private void CheckEnable()
        {
            NotifyOfPropertyChange(() => IsEnableDragEnd);
            NotifyOfPropertyChange(() => IsEnableDragPathControl);
            NotifyOfPropertyChange(() => IsStartObject);
        }

        public ConnectableObjectOperationViewModel(ConnectableObjectBase obj)
        {
            ConnectableObject = obj;
        }

        public void Border_MouseMove(ActionExecutionContext e)
        {
            ProcessDragStart(e, DragActionType.DropNext);
        }

        public void Border_MouseMove4(ActionExecutionContext e)
        {
            ProcessDragStart(e, DragActionType.DropCurvePathControl);
        }

        public void Border_MouseMove2(ActionExecutionContext e)
        {
            ProcessDragStart(e, DragActionType.DropEnd);
        }

        public void Border_MouseMove3(ActionExecutionContext e)
        {
            ProcessDragStart(e, DragActionType.Split);
        }

        public void Interpolate(ActionExecutionContext e)
        {
            var genStarts = RefStartObject.InterpolateCurve(RefStartObject.CurveInterpolaterFactory).ToArray();

            var editor = IoC.Get<IFumenObjectPropertyBrowser>().Editor;
            editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("插值曲线", () =>
            {
                editor.Fumen.RemoveObject(RefStartObject);
                foreach (var start in genStarts)
                    editor.Fumen.AddObject(start);
                editor.Redraw(RedrawTarget.OngekiObjects);
            }, () =>
            {
                foreach (var start in genStarts)
                    editor.Fumen.RemoveObject(start);
                editor.Fumen.AddObject(RefStartObject);
                editor.Redraw(RedrawTarget.OngekiObjects);
            }));
        }

        public abstract ConnectableChildObjectBase GenerateChildObject(bool needNext);

        private void ProcessDragStart(ActionExecutionContext e, DragActionType actionType)
        {
            if ((!_draggingItem) || RefStartObject is null)
                return;

            var arg = e.EventArgs as MouseEventArgs;

            Point mousePosition = arg.GetPosition(null);
            Vector diff = _mouseStartPosition - mousePosition;

            if (arg.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                //ConnectableObjectDropAction
                var genChildLazy = new Lazy<ConnectableChildObjectBase>(() => GenerateChildObject(actionType == DragActionType.DropNext));
                IEditorDropHandler dropAction = actionType switch
                {
                    DragActionType.DropNext or DragActionType.DropEnd => new ConnectableObjectDropAction(RefStartObject, genChildLazy.Value, () => CheckEnable()),
                    DragActionType.Split => new ConnectableObjectSplitDropAction(RefStartObject, genChildLazy.Value, () => CheckEnable()),
                    DragActionType.DropCurvePathControl => new AddLaneCurvePathControlDropAction(ConnectableObject as ConnectableChildObjectBase),
                    _ => default
                };

                DragDrop.DoDragDrop(e.Source, new DataObject(ToolboxDragDrop.DataFormat, dropAction), DragDropEffects.Move);
                _draggingItem = false;
            }
        }

        public void Border_MouseLeftButtonDown(ActionExecutionContext e)
        {
            var arg = e.EventArgs as MouseEventArgs;

            if (arg.LeftButton != MouseButtonState.Pressed)
                return;

            _mouseStartPosition = arg.GetPosition(null);
            _draggingItem = true;
        }

        public async void OnBrushButtonClick()
        {
            var editor = IoC.Get<IFumenObjectPropertyBrowser>().Editor;
            var fumen = editor.Fumen;

            if (RefStartObject?.IsPathVaild() != true)
            {
                MessageBox.Show("此轨道包含非法路径");
                return;
            }

            if (editor.CurrentCopiedSources.Count() > 1)
            {
                MessageBox.Show("因为已复制的物件超过一个，无法使用刷子功能");
                return;
            }

            if (editor.CurrentCopiedSources.Count() < 1)
            {
                MessageBox.Show("需要先复制一个可以复制的物件，才能使用刷子功能");
                return;
            }

            var copiedObjectViewModel = editor.CurrentCopiedSources.FirstOrDefault() as OngekiObjectBase;

            if (copiedObjectViewModel?.CopyNew(fumen) is null)
            {
                MessageBox.Show("此复制的物件无法使用刷子功能");
                return;
            }

            var dialog = new BrushTGridRangeDialogViewModel();
            dialog.BeginTGrid = RefStartObject.MinTGrid;
            dialog.EndTGrid = RefStartObject.MaxTGrid;

            if ((await IoC.Get<IWindowManager>().ShowDialogAsync(dialog)) != true)
                return;

            var beginTGrid = dialog.BeginTGrid;
            var endTGrid = dialog.EndTGrid;

            var redoAction = new System.Action(() => { });
            var undoAction = new System.Action(() => { });

            foreach ((var tGrid, _, _, _, _) in TGridCalculator.GetVisbleTimelines(
                fumen.BpmList,
                fumen.MeterChanges,
                TGridCalculator.ConvertTGridToY(beginTGrid, editor),
                TGridCalculator.ConvertTGridToY(endTGrid, editor),
                0,
                editor.Setting.BeatSplit,
                editor.Setting.VerticalDisplayScale,
                240))
            {
                var obj = copiedObjectViewModel.CopyNew(fumen);
                var xGrid = RefStartObject.CalulateXGrid(tGrid);

                if (xGrid is null)
                    continue;

                redoAction += () =>
                {
                    if (obj is ITimelineObject timelineObject)
                        timelineObject.TGrid = tGrid;
                    if (obj is IHorizonPositionObject horizonPositionObject)
                        horizonPositionObject.XGrid = xGrid;

                    editor.Fumen.AddObject(obj);
                };
                undoAction += () =>
                {
                    editor.RemoveObject(obj);
                };
            }

            redoAction += () => editor.Redraw(RedrawTarget.OngekiObjects);
            undoAction += () => editor.Redraw(RedrawTarget.OngekiObjects);

            editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("批量粘贴刷子", redoAction, undoAction));
        }

        public void OnPartChildCurveInterpolateClick()
        {
            PartChildCurveInterpolate();
        }

        public void PartChildCurveInterpolate()
        {
            var childObj = ConnectableObject as ConnectableChildObjectBase;

            if (!childObj.CheckCurveVaild())
            {
                MessageBox.Show("无法对非法的线段进行局部插值");
                return;
            }

            var from = childObj;
            var to = childObj.ReferenceStartObject.Children.FindNextOrDefault(childObj);

            var genChildren = childObj.InterpolateCurveChildren(childObj.CurveInterpolaterFactory).ToList();

            var prev = childObj.PrevObject;
            genChildren.RemoveAll(x => x.TGrid >= from.TGrid || x.TGrid <= prev.TGrid);

            var editor = IoC.Get<IFumenObjectPropertyBrowser>().Editor;
            var storeBackupControlPoints = new List<LaneCurvePathControlObject>();

            editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("部分曲线插值", () =>
            {
                foreach (var newChild in genChildren)
                    childObj.ReferenceStartObject.InsertChildObject(newChild.TGrid, newChild);

                storeBackupControlPoints.AddRange(childObj.PathControls);
                foreach (var cp in storeBackupControlPoints)
                    childObj.RemoveControlObject(cp);

                editor.Redraw(RedrawTarget.OngekiObjects);
            }, () =>
            {
                foreach (var newChild in genChildren)
                    childObj.ReferenceStartObject.RemoveChildObject(newChild);

                foreach (var cp in storeBackupControlPoints)
                    childObj.AddControlObject(cp);
                storeBackupControlPoints.Clear();

                editor.Redraw(RedrawTarget.OngekiObjects);
            }));
        }
    }
}
