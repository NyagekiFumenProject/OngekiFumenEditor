using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{
    public abstract class LaneDockableViewModelBase<T> : DisplayObjectViewModelBase<T> where T : OngekiObjectBase, ILaneDockable, IDisplayableObject, new()
    {
        public virtual IEnumerable<ConnectableObjectBase> PickDockableObjects(FumenVisualEditorViewModel editor = default)
        {
            editor = editor ?? EditorViewModel;
            return editor.EditorViewModels
                    .OfType<DisplayObjectViewModelBase>()
                    .Select(x => x.ReferenceOngekiObject as ConnectableObjectBase)
                    .FilterNull();
        }

        public override void MoveCanvas(Point relativePoint)
        {
            var editor = EditorViewModel;
            var forceMagneticToLane = editor.Setting.ForceTapHoldMagneticDockToLane;
            var forceMagnetic = editor.Setting.ForceMagneticDock;
            var enableMoveTo = !forceMagneticToLane;

            if (CheckAndAdjustY(relativePoint.Y) is double y && TGridCalculator.ConvertYToTGrid(y, editor) is TGrid tGrid)
            {
                var closestLaneObject = PickDockableObjects(editor)
                    .Select(x => x switch
                    {
                        ConnectableChildObjectBase child => child.ReferenceStartObject as LaneStartBase,
                        LaneStartBase start => start,
                        _ => default
                    })
                    .FilterNull()
                    .Select(startObject => (CalculateConnectableObjectCurrentRelativeX(startObject, tGrid), startObject))
                    .Where(x => x.Item1 != null)
                    .Select(x => (Math.Abs(x.Item1.Value - relativePoint.X), x.Item1.Value, x.startObject))
                    .OrderBy(x => x.Item1)
                    .FirstOrDefault();

                var magneticDockDistance = forceMagneticToLane || forceMagnetic ? int.MaxValue : 8;

                if (closestLaneObject.startObject is not null && closestLaneObject.Item1 < magneticDockDistance)
                {
                    relativePoint.X = closestLaneObject.Value;
                    var tap = ReferenceOngekiObject as T;
                    tap.ReferenceLaneStart = closestLaneObject.startObject;
                    //Log.LogDebug($"auto dock to lane : {closestLaneObject.startObject}");

                    enableMoveTo = true;
                }
            }

            //如果ForceTapHoldMagneticDockToLane=true,则不需要这里钦定位置
            if (enableMoveTo)
                base.MoveCanvas(relativePoint);
        }

        protected virtual double? CalculateConnectableObjectCurrentRelativeX(ConnectableStartObject startObject, TGrid tGrid)
        {
            if (tGrid < startObject.TGrid)
                return default;

            var prev = startObject as ConnectableObjectBase;
            foreach (var cur in startObject.Children)
            {
                if (tGrid < cur.TGrid)
                {
                    //就在当前[prev,cur]范围内，那么就插值计算咯
                    var timeX = MathUtils.CalculateXFromBetweenObjects(prev, cur, EditorViewModel, tGrid);
                    return timeX;
                }

                prev = cur;
            }

            return default;
        }
    }
}
