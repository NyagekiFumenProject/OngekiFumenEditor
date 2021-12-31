using Caliburn.Micro;
using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
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
    public class WallOperationViewModel : PropertyChangedBase
    {
        private bool _draggingItem;
        private Point _mouseStartPosition;

        private WallBase wall;

        public WallBase Wall
        {
            get
            {
                return wall;
            }
            set
            {
                wall = value;
                NotifyOfPropertyChange(() => Wall);
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
            IsEnableDrag = !((Wall switch
            {
                WallStart start => start,
                WallNext next => next.ReferenceWall,
                _ => default,
            })?.Children.OfType<BeamEnd>().Any() ?? false);
        }

        public WallOperationViewModel(WallBase obj)
        {
            Wall = obj;
        }

        public void Border_MouseMove(ActionExecutionContext e)
        {
            ProcessDragStart(e, true);
        }

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
                    WallChildBase genWallChild = isWallNext ? new WallNext() : new WallEnd();
                    DisplayObjectViewModelBase genViewModel = isWallNext ? new WallNextViewModel() : new WallEndViewModel();
                    genViewModel.ReferenceOngekiObject = genWallChild;

                    if (Wall is WallStart beamStart)
                    {
                        beamStart.AddChildWallObject(genWallChild);
                    }
                    else if (Wall is WallNext { ReferenceWall: { } } beamNext1)
                    {
                        beamNext1.ReferenceWall.AddChildWallObject(genWallChild);
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
