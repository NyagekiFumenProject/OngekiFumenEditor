using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Kernel.Graphics;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.Lane
{
    internal abstract class WallLaneDrawTarget : LaneDrawingTargetBase
    {
        public static Vector4 LeftWallColor { get; } = new(181 / 255.0f, 156 / 255.0f, 231 / 255.0f, 255 / 255.0f);
        public static Vector4 RightWallColor { get; } = new(231 / 255.0f, 149 / 255.0f, 178 / 255.0f, 255 / 255.0f);

        public abstract Vector4 WallLaneColor { get; }
        public override int LineWidth => 6;
        public override Vector4 GetLanePointColor(ConnectableObjectBase obj) => WallLaneColor;
    }

    [Export(typeof(IDrawingTarget))]
    internal class WallLeftLaneDrawTarget : WallLaneDrawTarget
    {
        public override IEnumerable<string> DrawTargetID { get; } = new[] { "WLS" };
        public override Vector4 WallLaneColor { get; } = LeftWallColor;
    }

    [Export(typeof(IDrawingTarget))]
    internal class WallRightLaneDrawTarget : WallLaneDrawTarget
    {
        public override IEnumerable<string> DrawTargetID { get; } = new[] { "WRS" };
        public override Vector4 WallLaneColor { get; } = RightWallColor;
    }
}
