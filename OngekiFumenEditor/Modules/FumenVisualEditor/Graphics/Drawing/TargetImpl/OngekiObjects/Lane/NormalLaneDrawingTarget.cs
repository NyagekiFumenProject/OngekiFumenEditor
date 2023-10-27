using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Numerics;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.Lane
{
	public abstract class NormalLaneDrawingTarget : LaneDrawingTargetBase
	{

	}

	[Export(typeof(IFumenEditorDrawingTarget))]
	public class LeftLaneDrawTarget : NormalLaneDrawingTarget
	{
		public static Vector4 LaneColor { get; } = new(1, 0, 0, 1);

		public override Vector4 GetLanePointColor(ConnectableObjectBase obj) => LaneColor;
		public override IEnumerable<string> DrawTargetID { get; } = new[] { "LLS" };
	}

	[Export(typeof(IFumenEditorDrawingTarget))]
	public class CenterLaneDrawTarget : NormalLaneDrawingTarget
	{
		public static Vector4 LaneColor { get; } = new(0, 1, 0, 1);

		public override Vector4 GetLanePointColor(ConnectableObjectBase obj) => LaneColor;
		public override IEnumerable<string> DrawTargetID { get; } = new[] { "LCS" };
	}

	[Export(typeof(IFumenEditorDrawingTarget))]
	public class RightLaneDrawTarget : NormalLaneDrawingTarget
	{
		public static Vector4 LaneColor { get; } = new(0, 0, 1, 1);

		public override Vector4 GetLanePointColor(ConnectableObjectBase obj) => LaneColor;
		public override IEnumerable<string> DrawTargetID { get; } = new[] { "LRS" };
	}
}
