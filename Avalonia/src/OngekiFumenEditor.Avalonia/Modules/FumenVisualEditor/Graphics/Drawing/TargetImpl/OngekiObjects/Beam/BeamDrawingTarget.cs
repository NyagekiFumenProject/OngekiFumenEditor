using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using System.Collections.Generic;
using Injectio.Attributes;
using System.Numerics;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.Beam
{
	[RegisterSingleton<IFumenEditorDrawingTarget>]
	public class BeamDrawingTarget : LaneDrawingTargetBase<BeamStart>
	{
		public static Vector4 LaneColor { get; } = new(1, 1, 0, 1);

		public override DrawingVisible DefaultVisible => DrawingVisible.Design;

		public override Vector4 GetLanePointColor(ConnectableObjectBase obj) => LaneColor;
		public override IEnumerable<string> DrawTargetID { get; } = new[] { "BMS", "OBS" };
	}
}


