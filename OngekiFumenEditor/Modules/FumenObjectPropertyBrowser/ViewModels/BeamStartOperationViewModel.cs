using Caliburn.Micro;
using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels
{
    public class BeamStartOperationViewModel : PropertyChangedBase
    {
        private bool _draggingItem;
        private Point _mouseStartPosition;

        private BeamStart beamStart;

        public BeamStart BeamStart
        {
            get
            {
                return beamStart;
            }
            set
            {
                beamStart = value;
                NotifyOfPropertyChange(() => BeamStart);
            }
        }

        public BeamStartOperationViewModel(BeamStart obj)
        {
            BeamStart = obj;
        }

        public void Border_MouseMove(ActionExecutionContext e)
        {
            if (!_draggingItem)
                return;

            var arg = e.EventArgs as MouseEventArgs;

            // Get the current mouse position
            Point mousePosition = arg.GetPosition(null);
            Vector diff = _mouseStartPosition - mousePosition;

            if (arg.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                var obj = e.Source as FrameworkElement;
                var beamNext = new BeamNext();
                BeamStart?.AddChildBeamObject(beamNext);

                var dragData = new DataObject(ToolboxDragDrop.DataFormat, new OngekiObjectDropParam()
                {
                    OngekiObject = new BeamNextViewModel()
                    {
                        ReferenceOngekiObject = beamNext
                    }
                });
                DragDrop.DoDragDrop(obj, dragData, DragDropEffects.Move);
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
