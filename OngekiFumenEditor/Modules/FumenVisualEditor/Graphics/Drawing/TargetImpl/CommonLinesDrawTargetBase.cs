using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Utils.ObjectPool;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl
{
	public abstract class CommonLinesDrawTargetBase<T> : CommonBatchDrawTargetBase<T> where T : ConnectableStartObject
	{
		public virtual int LineWidth { get; } = 2;
		private ISimpleLineDrawing lineDrawing;
		private static VertexDash invailedDash = new VertexDash() { DashSize = 6, GapSize = 3 };

		public CommonLinesDrawTargetBase()
		{
			lineDrawing = IoC.Get<ISimpleLineDrawing>();
		}

		public abstract Vector4 GetLanePointColor(ConnectableObjectBase obj);

		public void FillLine(IFumenEditorDrawingContext target, T start)
		{
			var color = GetLanePointColor(start);

			using var d = ObjectPool<List<LineVertex>>.GetWithUsingDisposable(out var list, out _);
			list.Clear();
			VisibleLineVerticesQuery.QueryVisibleLineVertices(target, start, invailedDash, color, list);
			lineDrawing.Draw(target, list, LineWidth);
		}

		public override void DrawBatch(IFumenEditorDrawingContext target, IEnumerable<T> starts)
		{
			foreach (var laneStart in starts)
			{
				lineDrawing.Begin(target, LineWidth);
				FillLine(target, laneStart);
				lineDrawing.End();
			}
		}
	}
}
