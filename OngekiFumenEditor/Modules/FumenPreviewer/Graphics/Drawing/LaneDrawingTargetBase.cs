using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public abstract class LaneDrawingTargetBase<T> : CommonCapLinesDrawTargetBase<T> where T : LaneStartBase
    {
        public abstract Vector4 GetLanePointColor(ConnectableObjectBase obj, OngekiFumen fumen);

        public override void FillLine(List<LinePoint> list, T obj, OngekiFumen fumen)
        {
            LinePoint calc(ConnectableObjectBase o)
            {
                return new(
                    new((float)XGridCalculator.ConvertXGridToX(o.XGrid, 30, Previewer.ViewWidth, 1), (float)TGridCalculator.ConvertTGridToY(o.TGrid, fumen.BpmList, 1.0, 240)),
                    GetLanePointColor(o, fumen)
                    );
            }

            list.Add(calc(obj));
            foreach (var child in obj.Children)
                list.Add(calc(child));
        }
    }

    public abstract class LaneDrawingTargetBase : LaneDrawingTargetBase<LaneStartBase>
    {

    }
}
