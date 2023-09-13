using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OngekiFumenEditor.Utils;
using System.Windows;
using System.ComponentModel;
using OngekiFumenEditor.Base.OngekiObjects;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls
{
    public class HoldEndObjectInteractiveAction : DockableObjectInteractiveAction
    {
        public override IEnumerable<ConnectableObjectBase> PickDockableObjects(FumenVisualEditorViewModel editor = null)
        {
            var forceMagneticToLane = editor?.Setting.ForceTapHoldMagneticDockToLane ?? false;
            if (forceMagneticToLane)
                return base.PickDockableObjects(editor);
            return Enumerable.Empty<ConnectableObjectBase>();
        }

        public override void OnMoveCanvas(OngekiObjectBase obj, Point relativePoint, FumenVisualEditorViewModel editor)
        {
            var ry = CheckAndAdjustY((ITimelineObject)obj, relativePoint.Y, editor);
            if (ry is double y && TGridCalculator.ConvertYToTGrid_DesignMode(y, editor) is TGrid tGrid)
            {
                if (((obj as HoldEnd)?.ReferenceStartObject as Hold)?.ReferenceLaneStart is LaneStartBase start)
                {
                    var x = CalculateConnectableObjectCurrentRelativeX(start, tGrid, editor) ?? relativePoint.X;
                    relativePoint.X = x;
                    //Log.LogDebug($"auto lock to lane x: {x}");
                }
            }

            base.OnMoveCanvas(obj, relativePoint, editor);
        }

        public override double? CheckAndAdjustX(IHorizonPositionObject obj, double x, FumenVisualEditorViewModel editor)
        {
            return x;
        }
    }
}
