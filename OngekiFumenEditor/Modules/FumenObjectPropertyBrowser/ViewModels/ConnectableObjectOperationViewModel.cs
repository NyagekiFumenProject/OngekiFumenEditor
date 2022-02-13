using Caliburn.Micro;
using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels.DropActions;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.Views;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
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
                CheckEnableDragEnd();
            }
        }

        private bool isEnableDragEnd = true;
        public bool IsEnableDragEnd
        {
            get
            {
                return isEnableDragEnd;
            }
            set
            {
                isEnableDragEnd = value;
                NotifyOfPropertyChange(() => isEnableDragEnd);
            }
        }

        public ConnectableStartObject RefStartObject => ConnectableObject switch
        {
            ConnectableStartObject start => start,
            ConnectableNextObject next => next.ReferenceStartObject,
            _ => default,
        };

        private void CheckEnableDragEnd()
        {
            IsEnableDragEnd = !(RefStartObject?.Children.OfType<ConnectableEndObject>().Any() ?? false);
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
                //ConnectableObjectDropAction
                var genChild = GenerateChildObject(isWallNext);
                var dropAction = new ConnectableObjectDropAction(RefStartObject, genChild, () => CheckEnableDragEnd());

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
