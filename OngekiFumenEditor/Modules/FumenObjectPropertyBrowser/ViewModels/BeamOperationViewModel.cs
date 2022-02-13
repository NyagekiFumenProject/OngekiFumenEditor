using Caliburn.Micro;
using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
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
    public class BeamOperationViewModel : PropertyChangedBase
    {
        private bool _draggingItem;
        private Point _mouseStartPosition;

        private BeamBase beam;

        public BeamBase Beam
        {
            get
            {
                return beam;
            }
            set
            {
                beam = value;
                NotifyOfPropertyChange(() => Beam);
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
                if (p != value)
                    NotifyOfPropertyChange(() => IsEnableDrag);
            }
        }

        private void CheckEnableDrag()
        {
            IsEnableDrag = !((Beam switch
            {
                BeamStart start => start,
                BeamNext next => next.ReferenceBeam,
                _ => default,
            })?.Children.OfType<BeamEnd>().Any() ?? false);
        }

        public BeamOperationViewModel(BeamBase obj)
        {
            Beam = obj;
        }

        public void Border_MouseMove(ActionExecutionContext e)
        {
            ProcessDragStart(e, true);
        }

        private void ProcessDragStart(ActionExecutionContext e, bool isBeamNext)
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
                    BeamChildBase genBeamChild = isBeamNext ? new BeamNext() : new BeamEnd();
                    DisplayObjectViewModelBase genViewModel = isBeamNext ? new BeamNextViewModel() : new BeamEndViewModel();
                    genViewModel.ReferenceOngekiObject = genBeamChild;

                    if (Beam is BeamStart beamStart)
                    {
                        beamStart.AddChildBeamObject(genBeamChild);
                    }
                    else if (Beam is BeamNext { ReferenceBeam: { } } beamNext1)
                    {
                        beamNext1.ReferenceBeam.AddChildBeamObject(genBeamChild);
                    }

                    CheckEnableDrag();
                    return genViewModel;
                }));
                DragDrop.DoDragDrop(e.Source, dragData, DragDropEffects.Move);
                _draggingItem = false;
            }
        }

        public void Border_MouseMove2(ActionExecutionContext e)
        {
            ProcessDragStart(e, false);
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
