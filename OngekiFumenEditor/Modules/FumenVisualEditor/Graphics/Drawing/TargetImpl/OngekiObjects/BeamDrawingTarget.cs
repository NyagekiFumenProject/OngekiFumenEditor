using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.Lane;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Numerics;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects
{
    [Export(typeof(IDrawingTarget))]
    public class BeamDrawingTarget : LaneDrawingTargetBase<BeamStart>
    {
        public static Vector4 LaneColor { get; } = new(1, 1, 0, 1);

        public override Vector4 GetLanePointColor(ConnectableObjectBase obj) => LaneColor;
        public override IEnumerable<string> DrawTargetID { get; } = new[] { "BMS" };
    }
}
