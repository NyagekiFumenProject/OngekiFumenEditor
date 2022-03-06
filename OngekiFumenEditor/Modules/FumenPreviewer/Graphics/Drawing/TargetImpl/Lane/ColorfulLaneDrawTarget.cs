using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OpenTK.Mathematics;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl.Lane
{
    [Export(typeof(IDrawingTarget))]
    public class ColorfulLaneDrawTarget : LaneDrawingTargetBase
    {
        public override Vector4 GetLanePointColor(ConnectableObjectBase obj, OngekiFumen fumen)
        {
            var color = ((IColorfulLane)obj).ColorId.Color;
            return new(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
        }

        public override IEnumerable<string> DrawTargetID { get; } = new[] { "CLS" };
    }
}
