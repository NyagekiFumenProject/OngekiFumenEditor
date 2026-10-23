using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Properties;
using System.Collections.Generic;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls
{
    public class HoldObjectInteractiveAction : DockableObjectInteractiveAction
    {
        public override IEnumerable<ConnectableObjectBase> PickDockableObjects(FumenVisualEditorViewModel editor = null)
        {
            return base.PickDockableObjects(editor);
        }

        public override void OnMoveCanvas(OngekiObjectBase obj, Point relativePoint, FumenVisualEditorViewModel editor)
        {
            base.OnMoveCanvas(obj, relativePoint, editor);

            UpdateHoldEndXGrid(obj);
        }

        private void UpdateHoldEndXGrid(OngekiObjectBase obj)
        {
            var hold = obj as Hold;
            if (hold.HoldEnd is HoldEnd end && hold.ReferenceLaneStart is ConnectableStartObject start)
            {
                if (start.CalulateXGrid(end.TGrid) is XGrid xGrid)
                    end.XGrid = xGrid;
            }
        }

    }
}
