using Caliburn.Micro;
using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels.DropActions;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.Views;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.Attributes;
using System;
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
            ConnectableNextObject next => next.ReferenceStartObject,
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
            var genStarts = RefStartObject.InterpolateCurve(
                () => LambdaActivator.CreateInstance(RefStartObject.GetType()) as ConnectableStartObject,
                () => LambdaActivator.CreateInstance(RefStartObject.NextType) as ConnectableNextObject,
                () => LambdaActivator.CreateInstance(RefStartObject.EndType) as ConnectableEndObject).ToArray();

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
            if (!_draggingItem)
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
    }
}
