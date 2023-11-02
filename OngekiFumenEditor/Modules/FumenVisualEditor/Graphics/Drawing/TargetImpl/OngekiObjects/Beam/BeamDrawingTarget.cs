using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Numerics;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.Beam
{
	[Export(typeof(IFumenEditorDrawingTarget))]
	public class BeamDrawingTarget : LaneDrawingTargetBase<BeamStart>
	{
		public static Vector4 LaneColor { get; } = new(1, 1, 0, 1);

		public override DrawingVisible DefaultVisible => DrawingVisible.Design;

		public override Vector4 GetLanePointColor(ConnectableObjectBase obj) => LaneColor;
		public override IEnumerable<string> DrawTargetID { get; } = new[] { "BMS", "OBS" };
	}
}
