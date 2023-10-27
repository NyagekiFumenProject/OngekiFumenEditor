using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Numerics;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.Lane
{
	[Export(typeof(IFumenEditorDrawingTarget))]
	public class EnemyLaneDrawTarget : NormalLaneDrawingTarget
	{
		public override DrawingVisible DefaultVisible => DrawingVisible.Design;

		public static Vector4 LaneColor { get; } = new(1, 1, 0, 1);

		public override Vector4 GetLanePointColor(ConnectableObjectBase obj) => LaneColor;

		public override IEnumerable<string> DrawTargetID { get; } = new[] { "ENS" };
	}
}
