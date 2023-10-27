using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using System.Collections.Generic;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl
{
	public abstract class LaneDrawingTargetBase<T> : CommonLinesDrawTargetBase<T> where T : ConnectableStartObject
	{
		public override int DefaultRenderOrder => 100;

		public override void DrawBatch(IFumenEditorDrawingContext target, IEnumerable<T> objs)
		{
			base.DrawBatch(target, objs);
		}
	}

	public abstract class LaneDrawingTargetBase : LaneDrawingTargetBase<LaneStartBase>
	{

	}
}
