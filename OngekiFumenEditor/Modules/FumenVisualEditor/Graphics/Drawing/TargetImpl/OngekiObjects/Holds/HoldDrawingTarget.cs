using AngleSharp.Dom;
using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Numerics;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.Holds
{
	[Export(typeof(IFumenEditorDrawingTarget))]
	public class HoldDrawingTarget : CommonDrawTargetBase<Hold>
	{
		public override IEnumerable<string> DrawTargetID { get; } = new string[] { "HLD", "CHD", "XHD" };

		public override int DefaultRenderOrder => 500;

		private ILineDrawing lineDrawing;

		public HoldDrawingTarget() : base()
		{
			lineDrawing = IoC.Get<ILineDrawing>();
		}

		public override void Draw(IFumenEditorDrawingContext target, Hold hold)
		{
			var start = hold.ReferenceLaneStart;
			var holdEnd = hold.HoldEnd;
			var laneType = start?.LaneType;

			var color = laneType switch
			{
				LaneType.Left => new Vector4(1, 0, 0, 0.75f),
				LaneType.Center => new Vector4(0, 1, 0, 0.75f),
				LaneType.Right => new Vector4(0, 0, 1, 0.75f),
				LaneType.WallLeft => new Vector4(35 / 255.0f, 4 / 255.0f, 117 / 255.0f, 0.75f),
				LaneType.WallRight => new Vector4(136 / 255.0f, 3 / 255.0f, 152 / 255.0f, 0.75f),
				_ => new Vector4(1, 1, 1, 0.75f),
			};

			if (holdEnd != null)
			{
				Vector2 PostPoint2(double tGridUnit, double xGridUnit)
				{
					var x = (float)XGridCalculator.ConvertXGridToX(xGridUnit, target.Editor);
					var y = (float)target.ConvertToY(tGridUnit);

					return new(x, y);
				}

				var holdPoint = PostPoint2(hold.TGrid.TotalUnit, hold.XGrid.TotalUnit);
				var holdEndPoint = PostPoint2(holdEnd.TGrid.TotalUnit, holdEnd.XGrid.TotalUnit);

				using var d = ObjectPool<List<LineVertex>>.GetWithUsingDisposable(out var list, out _);
				list.Clear();
				VisibleLineVerticesQuery.QueryVisibleLineVertices(target, start, VertexDash.Solider, color, list);
				if (list.Count > 0)
				{
					while (holdPoint.Y > list[0].Point.Y)
						list.RemoveAt(0);
					list.Insert(0, new LineVertex(holdPoint, color, VertexDash.Solider));
					while (holdEndPoint.Y < list[list.Count - 1].Point.Y)
						list.RemoveAt(list.Count - 1);
					list.Add(new LineVertex(holdEndPoint, color, VertexDash.Solider));
				}

				lineDrawing.Draw(target, list, 13);
			}
		}
	}
}