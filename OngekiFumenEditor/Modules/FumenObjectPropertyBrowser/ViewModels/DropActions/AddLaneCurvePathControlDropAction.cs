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

            var isFirst = true;

            editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.AddCurveControlPoint, () =>
            {
                curveObject.AddControlObject(cachePathControl);
                editor.MoveObjectTo(cachePathControl, dragEndPoint);
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
