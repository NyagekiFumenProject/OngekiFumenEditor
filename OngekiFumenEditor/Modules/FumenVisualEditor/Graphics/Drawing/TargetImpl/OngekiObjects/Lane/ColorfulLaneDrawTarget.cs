using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Numerics;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.Lane
{
	[Export(typeof(IFumenEditorDrawingTarget))]
	public class ColorfulLaneDrawTarget : NormalLaneDrawingTarget
	{
		public override Vector4 GetLanePointColor(ConnectableObjectBase obj)
		{
			var color = ((IColorfulLane)obj).ColorId.Color;
			return new(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
		}

		public override IEnumerable<string> DrawTargetID { get; } = new[] { "CLS" };
	}
}
