using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Numerics;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    public sealed class AutoPlayFaderLaneDrawTarget : LaneDrawingTargetBase
    {
        public override IEnumerable<string> DrawTargetID { get; } = new[] { "[APFS]" };
        public static Vector4 LaneColor { get; } = new(255 / 255.0f, 69 / 255.0f, 0 / 255.0f, 255 / 255.0f);
        public override int LineWidth => Math.Clamp(Properties.EditorGlobalSetting.Default.AutoPlayFaderLineWidth, 1, 24);
        public override Vector4 GetLanePointColor(ConnectableObjectBase obj) => LaneColor;
    }
}
