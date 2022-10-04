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

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls
{
    internal class DockableObjectInteractiveAction : DefaultObjectInteractiveAction
    {
        public virtual IEnumerable<ConnectableObjectBase> PickDockableObjects(FumenVisualEditorViewModel editor = default)
        {
            return editor.Fumen.Lanes.FilterNull();
        }

        public override void OnMoveCanvas(OngekiObjectBase obj, Point relativePoint, FumenVisualEditorViewModel editor)
        {
            var forceMagneticToLane = editor.Setting.ForceTapHoldMagneticDockToLane;
            var forceMagnetic = editor.Setting.ForceMagneticDock;
            var enableMoveTo = !forceMagneticToLane;

            if (CheckAndAdjustY(relativePoint.Y, editor) is double y && TGridCalculator.ConvertYToTGrid(y, editor) is TGrid tGrid)
            {
                var closestLaneObject = PickDockableObjects(editor)
                    .Select(x => x switch
                    {
                        ConnectableChildObjectBase child => child.ReferenceStartObject as LaneStartBase,
                        LaneStartBase start => start,
                        _ => default
                    })
                    .FilterNull()
                    .Select(startObject => (CalculateConnectableObjectCurrentRelativeX(startObject, tGrid, editor), startObject))
                    .Where(x => x.Item1 != null)
                    .Select(x => (Math.Abs(x.Item1.Value - relativePoint.X), x.Item1.Value, x.startObject))
                    .OrderBy(x => x.Item1)
                    .FirstOrDefault();

                var magneticDockDistance = forceMagneticToLane || forceMagnetic ? int.MaxValue : 8;

                if (closestLaneObject.startObject is not null && closestLaneObject.Item1 < magneticDockDistance)
                {
                    relativePoint.X = closestLaneObject.Value;
                    ((ILaneDockable)obj).ReferenceLaneStart = closestLaneObject.startObject;
                    //Log.LogDebug($"auto dock to lane : {closestLaneObject.startObject}");

                    enableMoveTo = true;
                }
            }

            //如果ForceTapHoldMagneticDockToLane=true,则不需要这里钦定位置
            if (enableMoveTo)
                base.OnMoveCanvas(obj, relativePoint, editor);
        }

        public override double? CheckAndAdjustX(double x, FumenVisualEditorViewModel editor)
        {
            /*
            if (((ILaneDockable)obj).ReferenceLaneStart is ConnectableStartObject start)
                return x;
            */
            return base.CheckAndAdjustX(x, editor);
        }

        protected virtual double? CalculateConnectableObjectCurrentRelativeX(ConnectableStartObject startObject, TGrid tGrid, FumenVisualEditorViewModel editor)
        {
            if (tGrid < startObject.TGrid)
                return default;

            var xGrid = startObject.CalulateXGrid(tGrid);
            if (xGrid == null)
                return default;

            return XGridCalculator.ConvertXGridToX(xGrid, editor);
        }
    }
}
