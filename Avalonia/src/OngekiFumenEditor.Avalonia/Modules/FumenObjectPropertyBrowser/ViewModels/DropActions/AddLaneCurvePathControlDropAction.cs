using OngekiFumenEditor.Avalonia.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using Avalonia;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels.DropActions
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

            editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Lang.B.AddCurveControlPoint.ToLocalizedString(), () =>
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



