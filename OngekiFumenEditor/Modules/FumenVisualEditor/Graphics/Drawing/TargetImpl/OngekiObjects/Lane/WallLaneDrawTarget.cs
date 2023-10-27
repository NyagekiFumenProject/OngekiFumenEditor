using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Numerics;

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

	[Export(typeof(IFumenEditorDrawingTarget))]
	internal class WallLeftLaneDrawTarget : WallLaneDrawTarget
	{
		public override IEnumerable<string> DrawTargetID { get; } = new[] { "WLS" };
		public override Vector4 WallLaneColor { get; } = LeftWallColor;
	}

	[Export(typeof(IFumenEditorDrawingTarget))]
	internal class WallRightLaneDrawTarget : WallLaneDrawTarget
	{
		public override IEnumerable<string> DrawTargetID { get; } = new[] { "WRS" };
		public override Vector4 WallLaneColor { get; } = RightWallColor;
	}
}
