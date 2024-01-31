using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels.DropActions
{
    public class AddLaneCurvePathControlDropAction : IEditorDropHandler
    {
        private ConnectableChildObjectBase curveObject;
        private LaneCurvePathControlObject cachePathControl;

        public AddLaneCurvePathControlDropAction(ConnectableChildObjectBase obj)
        {
            curveObject = obj;
            cachePathControl = new LaneCurvePathControlObject();
        }

        public void Drop(FumenVisualEditorViewModel editor, Point dragEndPoint)
        {
            if (!editor.CheckAndNotifyIfPlaceBeyondDuration(dragEndPoint))
                return;

            var dragTGrid = TGridCalculator.ConvertYToTGrid_DesignMode(dragEndPoint.Y, editor);
            var dragXGrid = XGridCalculator.ConvertXToXGrid(dragEndPoint.X, editor);
            var isFirst = true;

            editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.AddCurveControlPoint, () =>
            {
                cachePathControl.TGrid = dragTGrid;
                cachePathControl.XGrid = dragXGrid;
                curveObject.AddControlObject(cachePathControl);
                if (isFirst)
                {
                    editor.NotifyObjectClicked(cachePathControl);
                    isFirst = false;
                }
            }, () =>
            {
                curveObject.RemoveControlObject(cachePathControl);
            }));
        }
    }
}
