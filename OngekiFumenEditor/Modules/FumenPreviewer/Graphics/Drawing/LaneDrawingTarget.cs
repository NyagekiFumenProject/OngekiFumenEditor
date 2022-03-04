using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public abstract class LaneDrawingTarget : CommonLinesDrawTargetBase<LaneStartBase>
    {
        public abstract Vector4 LaneColor { get; }

        public override void FillLine(List<LinePoint> list, LaneStartBase obj, OngekiFumen fumen)
        {
            LinePoint calc(OngekiMovableObjectBase o)
            {
                return new(new((float)XGridCalculator.ConvertXGridToX(o.XGrid, 30, Previewer.ViewWidth, 1), (float)TGridCalculator.ConvertTGridToY(o.TGrid, fumen.BpmList, 240)), LaneColor);
            }

            list.Add(calc(obj));
            foreach (var child in obj.Children)
                list.Add(calc(child));
        }
    }
}
