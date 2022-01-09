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
    public abstract class TapViewModelBase<T> : DisplayObjectViewModelBase<T> where T : Tap,new()
    {
        public virtual IEnumerable<ConnectableObjectBase> PickDockableObjects(FumenVisualEditorViewModel editor = default)
        {
            editor = editor ?? EditorViewModel;
            return editor.DisplayObjectList
                    .OfType<FrameworkElement>()
                    .Select(x => x.DataContext)
                    .OfType<DisplayObjectViewModelBase>()
                    .Select(x => x.ReferenceOngekiObject as ConnectableObjectBase)
                    .FilterNull();
        }

        public override void MoveCanvas(Point relativePoint)
        {
            var editor = EditorViewModel;
            var tGrid = TGridCalculator.ConvertYToTGrid(CheckAndAdjustY(relativePoint.Y), editor);

            if (tGrid is not null)
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

                if (closestLaneObject.startObject is not null && closestLaneObject.Item1 < 8)
                {
                    relativePoint.X = closestLaneObject.Value;
                    var tap = ReferenceOngekiObject as Tap;
                    tap.ReferenceLaneStart = closestLaneObject.startObject;
                    //Log.LogDebug($"auto dock to lane : {closestLaneObject.startObject}");
                }
            }

            base.MoveCanvas(relativePoint);
        }

        private double? CalculateConnectableObjectCurrentRelativeX(ConnectableStartObject startObject, TGrid tGrid)
        {
            if (tGrid < startObject.TGrid)
                return default;

            var prev = startObject as ConnectableObjectBase;
            foreach (var cur in startObject.Children)
            {
                if (tGrid < cur.TGrid)
                {
                    //就在当前[prev,cur]范围内，那么就插值计算咯
                    var prevX = XGridCalculator.ConvertXGridToX(prev.XGrid, EditorViewModel);
                    var prevY = TGridCalculator.ConvertTGridToY(prev.TGrid, EditorViewModel, false) ?? 0;
                    var curX = XGridCalculator.ConvertXGridToX(cur.XGrid, EditorViewModel);
                    var curY = TGridCalculator.ConvertTGridToY(cur.TGrid, EditorViewModel, false) ?? 0;

                    var timeY = TGridCalculator.ConvertTGridToY(tGrid, EditorViewModel) ?? 0;
                    var formula = MathUtils.BuildTwoPointFormFormula(prevX, prevY, curX, curY);
                    var timeX = formula(timeY);

                    return timeX;
                }

                prev = cur;
            }

            return default;
        }
    }
}
