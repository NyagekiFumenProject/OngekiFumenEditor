using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls
{
    internal class ConnectableObjectInteractiveAction : DefaultObjectInteractiveAction
    {
        public override void OnMoveCanvas(OngekiObjectBase o, Point point, FumenVisualEditorViewModel editor)
        {
            base.OnMoveCanvas(o, point, editor);
            var obj = o switch
            {
                ConnectableObjectBase co => co,
                LaneCurvePathControlObject ctrl => ctrl.RefCurveObject,
                _ => default
            };
            ConnectableObjectDockingHelper.RelocateDockableObjects(editor.Fumen, obj);
        }
    }
}
