using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl
{
	public static class VisibleLineVerticesQuery
	{
		public static void QueryVisibleLineVertices(IFumenEditorDrawingContext target, ConnectableStartObject start, VertexDash invailedDash, Vector4 color, IList<LineVertex> outVertices)
		{
			if (start is null)
				return;

			var resT = start.TGrid.ResT;
			var resX = start.XGrid.ResX;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			void PostPoint2(double tGridUnit, double xGridUnit, bool isVailed)
			{
				var x = (float)XGridCalculator.ConvertXGridToX(xGridUnit, target.Editor);
				var y = (float)target.ConvertToY(tGridUnit);

				outVertices.Add(new(new(x, y), color, isVailed ? VertexDash.Solider : invailedDash));
			}
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			void PostPoint(TGrid tGrid, XGrid xGrid, bool isVailed) => PostPoint2(tGrid.TotalUnit, xGrid.TotalUnit, isVailed);
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			void PostObject(OngekiMovableObjectBase obj, bool isVailed) => PostPoint(obj.TGrid, obj.XGrid, isVailed);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			bool getNextIsVaild(ConnectableObjectBase o) => o.NextObject?.IsVaildPath ?? true;

			var prevVisible = target.CheckVisible(start.TGrid);
			var alwaysDrawing = target.CheckRangeVisible(start.MinTGrid, start.MaxTGrid);

			PostObject(start, getNextIsVaild(start));
			var prevInvaild = true;
			var prevObj = start as ConnectableObjectBase;

			foreach (var childObj in start.Children)
			{
				var visible = alwaysDrawing || target.CheckVisible(childObj.TGrid);
				var curIsVaild = childObj.IsVaildPath;
				if (prevInvaild != curIsVaild)
				{
					PostObject(prevObj, curIsVaild);
					prevInvaild = curIsVaild;
				}

				if (prevVisible != visible && prevVisible == false)
					PostObject(prevObj, prevInvaild);

				if (visible || prevVisible)
				{
					if (childObj.IsCurvePath)
					{
						foreach (var item in childObj.GetConnectionPaths())
							PostPoint2(item.pos.Y / resT, item.pos.X / resX, curIsVaild);
					}
					else
						PostObject(childObj, curIsVaild);
				}

				prevObj = childObj;
				prevVisible = visible;
			}
		}
	}
}
