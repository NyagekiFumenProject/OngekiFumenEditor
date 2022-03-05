using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl.Lane
{
    [Export(typeof(IDrawingTarget))]
    public class LeftLaneDrawTarget : LaneDrawingTargetBase
    {
        public override Vector4 GetLanePointColor(ConnectableObjectBase obj, OngekiFumen fumen) => new(1, 0, 0, 1);
        public override IEnumerable<string> DrawTargetID { get; } = new[] { "LLS" };
    }

    [Export(typeof(IDrawingTarget))]
    public class CenterLaneDrawTarget : LaneDrawingTargetBase
    {
        public override Vector4 GetLanePointColor(ConnectableObjectBase obj, OngekiFumen fumen) => new(0, 1, 0, 1);
        public override IEnumerable<string> DrawTargetID { get; } = new[] { "LCS" };
    }

    [Export(typeof(IDrawingTarget))]
    public class RightLaneDrawTarget : LaneDrawingTargetBase
    {
        public override Vector4 GetLanePointColor(ConnectableObjectBase obj, OngekiFumen fumen) => new(0, 0, 1, 1);
        public override IEnumerable<string> DrawTargetID { get; } = new[] { "LRS" };
    }

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
