using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls
{
    public class HoldEndObjectInteractiveAction : DockableObjectInteractiveAction
    {
        private bool isHoldEndSnappedToLaneThisMove;

        protected override bool IsXGridSnappedToLaneThisMove
            => base.IsXGridSnappedToLaneThisMove || isHoldEndSnappedToLaneThisMove;

        public override IEnumerable<ConnectableObjectBase> PickDockableObjects(FumenVisualEditorViewModel editor = null)
        {
            var forceMagneticToLane = editor?.Setting.ForceTapHoldMagneticDockToLane ?? false;
            if (forceMagneticToLane)
                return base.PickDockableObjects(editor);
            return Enumerable.Empty<ConnectableObjectBase>();
        }

        public override void OnMoveCanvas(OngekiObjectBase obj, Point relativePoint, FumenVisualEditorViewModel editor)
        {
            isHoldEndSnappedToLaneThisMove = false;

            var ry = CheckAndAdjustY((ITimelineObject)obj, relativePoint.Y, editor);
            if (ry is double y && editor.ConvertYToTGrid_DesignMode(y) is TGrid tGrid)
            {
                if (((obj as HoldEnd)?.RefHold)?.ReferenceLaneStart is LaneStartBase start)
                {
                    var laneX = CalculateConnectableObjectCurrentRelativeX(start, tGrid, editor);
                    if (TryGetHorizontalSegmentDockingX(start, tGrid, relativePoint.X, editor, out var horizontalX))
                    {
                        relativePoint.X = horizontalX;
                        isHoldEndSnappedToLaneThisMove = true;
                    }
                    else if (laneX is double lx)
                    {
                        relativePoint.X = lx;
                        isHoldEndSnappedToLaneThisMove = true;
                    }
                }
            }

            base.OnMoveCanvas(obj, relativePoint, editor);
        }

        public override double? CheckAndAdjustX(IHorizonPositionObject obj, double x, FumenVisualEditorViewModel editor)
        {
            return base.CheckAndAdjustX(obj, x, editor);
        }
    }
}
