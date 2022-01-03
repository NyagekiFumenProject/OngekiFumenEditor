using Caliburn.Micro;
using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.Views;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
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
                CheckEnableDrag();
            }
        }

        private bool isEnableDrag = true;
        public bool IsEnableDrag
        {
            get
            {
                return isEnableDrag;
            }
            set
            {
                var p = isEnableDrag;
                isEnableDrag = value;
                NotifyOfPropertyChange(() => IsEnableDrag);
            }
        }

        private void CheckEnableDrag()
        {
            IsEnableDrag = !((ConnectableObject switch
            {
                ConnectableStartObject start => start,
                ConnectableNextObject next => next.ReferenceStartObject,
                _ => default,
            })?.Children.OfType<ConnectableEndObject>().Any() ?? false);
        }

        public ConnectableObjectOperationViewModel(ConnectableObjectBase obj)
        {
            ConnectableObject = obj;
        }

        public void Border_MouseMove(ActionExecutionContext e)
        {
            ProcessDragStart(e, true);
        }

        public void Border_MouseMove2(ActionExecutionContext e)
        {
            ProcessDragStart(e, false);
        }

        public abstract ConnectableChildObjectBase GenerateChildObject(bool needNext);
        public abstract DisplayObjectViewModelBase GenerateChildObjectViewModel(bool needNext);

        private void ProcessDragStart(ActionExecutionContext e, bool isWallNext)
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
                var dragData = new DataObject(ToolboxDragDrop.DataFormat, new OngekiObjectDropParam(() =>
                {
                    var genWallChild = GenerateChildObject(isWallNext);
                    var genViewModel = GenerateChildObjectViewModel(isWallNext);
                    genViewModel.ReferenceOngekiObject = genWallChild;

                    if (ConnectableObject is ConnectableStartObject start)
                    {
                        start.AddChildWallObject(genWallChild);
                    }
                    else if (ConnectableObject is ConnectableNextObject { ReferenceStartObject: { } } next)
                    {
                        next.ReferenceStartObject.AddChildWallObject(genWallChild);
                    }

                    CheckEnableDrag();
                    return genViewModel;
                }));
                DragDrop.DoDragDrop(e.Source, dragData, DragDropEffects.Move);
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
