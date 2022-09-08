using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public abstract class LaneDrawingTargetBase<T> : CommonLinesDrawTargetBase<T> where T : LaneStartBase
    {
        public abstract Vector4 GetLanePointColor(ConnectableObjectBase obj);

        public override void FillLine(IFumenPreviewer target, List<LineVertex> list, T obj)
        {
            LineVertex calc(ConnectableObjectBase o)
            {
                return new(
                    new((float)XGridCalculator.ConvertXGridToX(o.XGrid, 30, target.ViewWidth, 1), (float)TGridCalculator.ConvertTGridToY(o.TGrid, target.Fumen.BpmList, 1.0, 240)),
                    GetLanePointColor(o)
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
