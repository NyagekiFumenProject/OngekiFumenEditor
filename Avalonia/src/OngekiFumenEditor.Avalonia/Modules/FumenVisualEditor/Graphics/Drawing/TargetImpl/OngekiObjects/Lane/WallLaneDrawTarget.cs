using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using System.Collections.Generic;
using Injectio.Attributes;
using System.Numerics;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.Lane
{
    internal abstract class WallLaneDrawTarget : LaneDrawingTargetBase
    {
        public static Vector4 LeftWallColor { get; } = new(181 / 255.0f, 156 / 255.0f, 231 / 255.0f, 255 / 255.0f);
        public static Vector4 RightWallColor { get; } = new(231 / 255.0f, 149 / 255.0f, 178 / 255.0f, 255 / 255.0f);

        public abstract Vector4 WallLaneColor { get; }
        public override int LineWidth => 6;
        public override Vector4 GetLanePointColor(ConnectableObjectBase obj) => WallLaneColor;

        public override void DrawBatch(IFumenEditorDrawingContext target, IEnumerable<LaneStartBase> starts)
        {
            if (target.Editor.IsPreviewMode && target.Editor.HideWallLaneWhenEnablePlayField)
                return;
            base.DrawBatch(target, starts);
        }
    }

    [RegisterSingleton<IFumenEditorDrawingTarget>]
    internal class WallLeftLaneDrawTarget : WallLaneDrawTarget
    {
        public override IEnumerable<string> DrawTargetID { get; } = new[] { "WLS" };
        public override Vector4 WallLaneColor { get; } = LeftWallColor;
    }

    [RegisterSingleton<IFumenEditorDrawingTarget>]
    internal class WallRightLaneDrawTarget : WallLaneDrawTarget
    {
        public override IEnumerable<string> DrawTargetID { get; } = new[] { "WRS" };
        public override Vector4 WallLaneColor { get; } = RightWallColor;
    }
}


