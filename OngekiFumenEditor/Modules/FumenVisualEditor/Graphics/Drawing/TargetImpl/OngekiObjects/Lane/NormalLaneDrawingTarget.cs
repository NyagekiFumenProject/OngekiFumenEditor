using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor;
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
    public abstract class NormalLaneDrawingTarget : LaneDrawingTargetBase
    {

    }

    [Export(typeof(IDrawingTarget))]
    public class LeftLaneDrawTarget : NormalLaneDrawingTarget
    {
        public static Vector4 LaneColor { get; } = new(1, 0, 0, 1);

        public override Vector4 GetLanePointColor(ConnectableObjectBase obj) => LaneColor;
        public override IEnumerable<string> DrawTargetID { get; } = new[] { "LLS" };
    }

    [Export(typeof(IDrawingTarget))]
    public class CenterLaneDrawTarget : NormalLaneDrawingTarget
    {
        public static Vector4 LaneColor { get; } = new(0, 1, 0, 1);

        public override Vector4 GetLanePointColor(ConnectableObjectBase obj) => LaneColor;
        public override IEnumerable<string> DrawTargetID { get; } = new[] { "LCS" };
    }

    [Export(typeof(IDrawingTarget))]
    public class RightLaneDrawTarget : NormalLaneDrawingTarget
    {
        public static Vector4 LaneColor { get; } = new(0, 0, 1, 1);

        public override Vector4 GetLanePointColor(ConnectableObjectBase obj) => LaneColor;
        public override IEnumerable<string> DrawTargetID { get; } = new[] { "LRS" };
    }
}
