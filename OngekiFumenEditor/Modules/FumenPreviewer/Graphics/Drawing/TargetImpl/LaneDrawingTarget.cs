using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl
{
    public abstract class LaneDrawingTarget : CommonLinesDrawTargetBase<LaneStartBase>
    {
        public abstract Vector4 LaneColor { get; }

        public override void FillLine(List<LinePoint> appendFunc, LaneStartBase obj, OngekiFumen fumen)
        {
            throw new NotImplementedException();
        }
    }
}
