using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Numerics;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.Lane
{
    [Export(typeof(IDrawingTarget))]
    public class EnemyLaneDrawTarget : NormalLaneDrawingTarget
    {
        public static Vector4 LaneColor { get; } = new(1, 1, 0, 1);

        public override Vector4 GetLanePointColor(ConnectableObjectBase obj) => LaneColor;

        public override IEnumerable<string> DrawTargetID { get; } = new[] { "ENS" };
    }
}
