using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using Gemini.Modules.Toolbox;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{
    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Tap", "Ongeki Objects")]
    public class TapViewModel : DisplayObjectViewModelBase<Tap>
    {
        public override void MoveCanvas(Point relativePoint)
        {
            var editor = EditorViewModel;
            var tGrid = TGridCalculator.ConvertYToTGrid(CheckAndAdjustY(relativePoint.Y), editor);

            if (tGrid is not null)
            {

                var connectableXList = editor.DisplayObjectList
                    .OfType<FrameworkElement>()
                    .Select(x => x.DataContext)
                    .OfType<DisplayObjectViewModelBase>()
                    .Select(x => x.ReferenceOngekiObject)
                    .FilterNull()
                    .Select(x => x switch
                    {
                        ConnectableChildObjectBase child => child.ReferenceStartObject,
                        ConnectableStartObject start => start,
                        _ => default
                    })
                    .FilterNull()
                    .Select(startObject => (CalculateConnectableObjectCurrentRelativeX(startObject, tGrid), startObject))
                    .Where(x => x.Item1 != null)
                    .Select(x => (Math.Abs(x.Item1.Value - relativePoint.X), x.Item1.Value, x.startObject))
                    .OrderByDescending(x => x.Item1)
                    .FirstOrDefault();

                if (connectableXList.startObject is not null && connectableXList.Item1 < 5)
                {
                    relativePoint.X = connectableXList.Value;
                    //todo apply lane* object to tap object.
                }
                if (connectableXList.startObject is not null)
                {
                    Log.LogDebug($"dist:{connectableXList.Item1} pos:{connectableXList.Value}");
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
