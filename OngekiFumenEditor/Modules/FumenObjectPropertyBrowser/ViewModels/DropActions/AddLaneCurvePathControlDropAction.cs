using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            var dragTGrid = TGridCalculator.ConvertYToTGrid(dragEndPoint.Y, editor);
            var dragXGrid = XGridCalculator.ConvertXToXGrid(dragEndPoint.X, editor);
            var isFirst = true;

            editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("添加曲线控制点", () =>
            {
                cachePathControl.TGrid = dragTGrid;
                cachePathControl.XGrid = dragXGrid;
                curveObject.AddControlObject(cachePathControl);
                editor.Redraw(RedrawTarget.OngekiObjects);
                if (isFirst)
                {
                    editor.NotifyObjectClicked(cachePathControl);
                    isFirst = false;
                }
            }, () =>
            {
                curveObject.RemoveControlObject(cachePathControl);
                editor.Redraw(RedrawTarget.OngekiObjects);
            }));
        }
    }
}
