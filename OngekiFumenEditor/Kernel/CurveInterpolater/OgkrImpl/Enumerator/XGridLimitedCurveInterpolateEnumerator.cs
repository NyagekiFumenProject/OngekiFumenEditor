using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Kernel.CurveInterpolater.DefaultImpl.Enumerator
{
	public class XGridLimitedCurveInterpolateEnumerator : DefaultCurveInterpolateEnumerator
	{
		public XGridLimitedCurveInterpolateEnumerator(ConnectableStartObject start) : base(start)
		{

		}

		public XGridLimitedCurveInterpolateEnumerator(ConnectableChildObjectBase from, ConnectableChildObjectBase to = null) : base(from, to)
		{

		}

		private IEnumerable<CurvePoint> InterpolateCore(ConnectableChildObjectBase x)
		{
			var itor = base.Interpolate(x).GetEnumerator();
			if (!itor.MoveNext())
				yield break;
			yield return itor.Current;
			var prev = itor.Current;
			var prevRetY = (float)prev.TGrid.TotalGrid / prev.TGrid.ResT;
			var prevAppendNewCornerPointFlag = default(float?);

			while (itor.MoveNext())
			{
				var cur = itor.Current;

				var prevXunit = prev.XGrid.TotalGrid * 1.0f / prev.XGrid.ResX;
				var prevXunitInt = (int)prevXunit;
				var curXunit = cur.XGrid.TotalGrid * 1.0f / cur.XGrid.ResX;
				var curXunitInt = (int)curXunit;
				var prevX = prev.XGrid.TotalGrid;
				var prevY = prev.TGrid.TotalGrid;
				var curX = cur.XGrid.TotalGrid;
				var curY = cur.TGrid.TotalGrid;

				//Log.LogDebug($"--------------");
				//Log.LogDebug($"current ({cur})");

				#region Append New Corner Point
				//当有个急转角，那么就判断这个转角的点是否也要补充
				/**
                        |     /     |
                        |    /      |
                        |   /       |
                        |  <  <-----|-------这里的转角点就判断是否要保留，如果超出1XGridUnit的75%的话就保留吧
                        |   \       |
                        |    \      |
                        |     \     |
                        |      \    | 
                        |       \   |
                        |        \  |
                        |         \ |
                        |          \|
                        |           |\
                        |           | \ 
                        |           |  \

                 */
				var appendNewCornerPointFlag = Math.Sign(curX - prevX);
				if (prevAppendNewCornerPointFlag is not null)
				{
					//转角判断
					if (appendNewCornerPointFlag * prevAppendNewCornerPointFlag < 0)
					{
						var rawXGridUnit = prev.XGrid.TotalGrid * 1.0 / prev.XGrid.ResX;
						var judge = rawXGridUnit - (int)rawXGridUnit;
						//转角位置判断是否要补转角点
						if (Math.Abs(judge) > 0.50)
						{
							var newXUnit = (int)rawXGridUnit + (judge > 0 ? 1 : -1);
							var newPoint = new CurvePoint()
							{
								XGrid = new XGrid(newXUnit, 0),
								TGrid = prev.TGrid.CopyNew()
							};
							//Log.LogDebug($"return new corner point ({newPoint})");
							yield return newPoint;
						}
					}
				}
				prevAppendNewCornerPointFlag = appendNewCornerPointFlag;
				#endregion

				var isZeroSpecial = prevXunitInt == curXunitInt && curXunitInt == 0 && prevXunit * curXunit < 0;

				if (curXunit == curXunitInt)
				{
					//Log.LogDebug($"return ({cur}) directly because curXunitInt == curXunit");
					prevRetY = curY * 1f / cur.TGrid.ResT;
					yield return cur;
				}
				else if (prevXunitInt != curXunitInt || isZeroSpecial)
				{
					//Log.LogDebug($"begin interpolate from ({prev}) to ({cur})");

					foreach (var i in MathUtils.GetIntegersBetweenTwoValues(prevXunit, curXunit))
					{
						/*
                            核心思想，存在两个点prev/cur
                            这两个点之间刚好穿过一个或者多个xunitLine
                            那么就通过这两个点构造两点式一次函数,插值算出经过XGridUnit对应的点cp并返回

                                 calculate there between cp1 and cp2
                                  |         |
                                  v         v
                           
               CurvePoint2(prev)  |         |        X[2.5,0]
               -------------o-----|---------|--------o----------------------
                      X[0.5,0]    |         |          CurvePoint2(cur)
                                  |         |
                         xunitLine1 X[1,0]   xunitLine1 X[2,0] 
                    */
						var xGrid = new XGrid(i, 0);
						var y = MathUtils.CalculateYFromTwoPointFormFormula(xGrid.TotalGrid, prevX, prevY, curX, curY);
						var tunit = (float)(y / prev.TGrid.ResT);
						var tGrid = new TGrid(tunit, 0);
						//Log.LogDebug($"interpolate xunit:{i} from ({prev}) to ({cur})");

						if (Math.Abs(prevRetY - tunit) > 0.0001)
						{
							var point = new CurvePoint()
							{
								XGrid = xGrid,
								TGrid = tGrid,
							};
							//Log.LogDebug($"return new interpolated point: ({point})");
							yield return point;
						}
						else
						{
							//Log.LogDebug($"return Math.Abs(prevRetY({prevRetY}) - tunit({tunit})) < 0.01");
						}
						prevRetY = tunit;
					}
				}
				else
				{
					//Log.LogDebug($"return nothing prevXunitInt({prevXunitInt}) == curXunitInt({curXunitInt})");
				}

				prev = cur;
			}
			yield return prev;
		}

		protected override IEnumerable<CurvePoint> Interpolate(ConnectableChildObjectBase x)
		{
			return InterpolateCore(x);
		}

		public override CurvePoint? EnumerateNext()
		{
			if (base.EnumerateNext() is not CurvePoint p)
				return default;

			return new CurvePoint()
			{
				TGrid = p.TGrid,
				XGrid = new XGrid((int)(p.XGrid.TotalGrid * 1.0f / p.XGrid.ResX)),
			};
		}
	}
}
