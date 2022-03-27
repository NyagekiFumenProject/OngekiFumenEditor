using Caliburn.Micro;
using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base.EditorObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels.DropActions;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels
{
    public class LaneCurveOperationViewModel : PropertyChangedBase
    {
        private bool _draggingItem;
        private Point _mouseStartPosition;
        private LaneCurveObject laneCurveObject;

        public LaneCurveOperationViewModel(LaneCurveObject laneCurveObject)
        {
            this.laneCurveObject = laneCurveObject;
        }

        public void Border_MouseMove(ActionExecutionContext e)
        {
            ProcessDragStart(e);
        }

        private void ProcessDragStart(ActionExecutionContext e)
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
                IEditorDropHandler dropAction = new AddLaneCurvePathControlDropAction(laneCurveObject);

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
