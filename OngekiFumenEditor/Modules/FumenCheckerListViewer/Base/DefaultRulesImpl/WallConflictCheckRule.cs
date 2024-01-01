using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
	[Export(typeof(IFumenCheckRule))]
	internal class WallConflictCheckRule : IFumenCheckRule
	{
		const string RuleName = "WallConflict";

		private struct WallInfo
		{
			public LaneStartBase Wall { get; set; }
			public GridRange TGridRange { get; set; }
			public GridRange XGridRange { get; set; }
		}

		private struct Line
		{
			public double x1 { get; set; }
			public double y1 { get; set; }

			public double x2 { get; set; }
			public double y2 { get; set; }

			public override string ToString() => $"({x1:F2},{y1:F2})-({x2:F2},{y2:F2})";
		}

		private struct Point
		{
			public double x { get; set; }
			public double y { get; set; }
		}

		private class LineIntersection
		{
			//  Returns Point of intersection if do intersect otherwise default Point (null)
			public static Point? FindIntersection(Line lineA, Line lineB, double tolerance = 0.001)
			{
				double x1 = lineA.x1, y1 = lineA.y1;
				double x2 = lineA.x2, y2 = lineA.y2;

				double x3 = lineB.x1, y3 = lineB.y1;
				double x4 = lineB.x2, y4 = lineB.y2;

				// equations of the form x = c (two vertical lines)
				if (Math.Abs(x1 - x2) < tolerance && Math.Abs(x3 - x4) < tolerance && Math.Abs(x1 - x3) < tolerance)
				{
					return new() { x = x1, y = y3 };
				}

				//equations of the form y=c (two horizontal lines)
				if (Math.Abs(y1 - y2) < tolerance && Math.Abs(y3 - y4) < tolerance && Math.Abs(y1 - y3) < tolerance)
				{
					return new() { x = x1, y = y3 };
				}

				//equations of the form x=c (two vertical parallel lines)
				if (Math.Abs(x1 - x2) < tolerance && Math.Abs(x3 - x4) < tolerance)
				{
					//return default (no intersection)
					return null;
				}

				//equations of the form y=c (two horizontal parallel lines)
				if (Math.Abs(y1 - y2) < tolerance && Math.Abs(y3 - y4) < tolerance)
				{
					//return default (no intersection)
					return null;
				}

				//general equation of line is y = mx + c where m is the slope
				//assume equation of line 1 as y1 = m1x1 + c1 
				//=> -m1x1 + y1 = c1 ----(1)
				//assume equation of line 2 as y2 = m2x2 + c2
				//=> -m2x2 + y2 = c2 -----(2)
				//if line 1 and 2 intersect then x1=x2=x & y1=y2=y where (x,y) is the intersection point
				//so we will get below two equations 
				//-m1x + y = c1 --------(3)
				//-m2x + y = c2 --------(4)

				double x, y;

				//lineA is vertical x1 = x2
				//slope will be infinity
				//so lets derive another solution
				if (Math.Abs(x1 - x2) < tolerance)
				{
					//compute slope of line 2 (m2) and c2
					double m2 = (y4 - y3) / (x4 - x3);
					double c2 = -m2 * x3 + y3;

					//equation of vertical line is x = c
					//if line 1 and 2 intersect then x1=c1=x
					//subsitute x=x1 in (4) => -m2x1 + y = c2
					// => y = c2 + m2x1 
					x = x1;
					y = c2 + m2 * x1;
				}
				//lineB is vertical x3 = x4
				//slope will be infinity
				//so lets derive another solution
				else if (Math.Abs(x3 - x4) < tolerance)
				{
					//compute slope of line 1 (m1) and c2
					double m1 = (y2 - y1) / (x2 - x1);
					double c1 = -m1 * x1 + y1;

					//equation of vertical line is x = c
					//if line 1 and 2 intersect then x3=c3=x
					//subsitute x=x3 in (3) => -m1x3 + y = c1
					// => y = c1 + m1x3 
					x = x3;
					y = c1 + m1 * x3;
				}
				//lineA & lineB are not vertical 
				//(could be horizontal we can handle it with slope = 0)
				else
				{
					//compute slope of line 1 (m1) and c2
					double m1 = (y2 - y1) / (x2 - x1);
					double c1 = -m1 * x1 + y1;

					//compute slope of line 2 (m2) and c2
					double m2 = (y4 - y3) / (x4 - x3);
					double c2 = -m2 * x3 + y3;

					//solving equations (3) & (4) => x = (c1-c2)/(m2-m1)
					//plugging x value in equation (4) => y = c2 + m2 * x
					x = (c1 - c2) / (m2 - m1);
					y = c2 + m2 * x;

					//verify by plugging intersection point (x, y)
					//in orginal equations (1) & (2) to see if they intersect
					//otherwise x,y values will not be finite and will fail this check
					if (!(Math.Abs(-m1 * x + y - c1) < tolerance
						&& Math.Abs(-m2 * x + y - c2) < tolerance))
					{
						//return default (no intersection)
						return null;
					}
				}

				//x,y can intersect outside the line segment since line is infinitely long
				//so finally check if x, y is within both the line segments
				if (IsInsideLine(lineA, x, y) &&
					IsInsideLine(lineB, x, y))
				{
					return new Point { x = x, y = y };
				}

				//return default (no intersection)
				return null;

			}

			// Returns true if given point(x,y) is inside the given line segment
			private static bool IsInsideLine(Line line, double x, double y)
			{
				return (x >= line.x1 && x <= line.x2
							|| x >= line.x2 && x <= line.x1)
					   && (y >= line.y1 && y <= line.y2
							|| y >= line.y2 && y <= line.y1);
			}
		}

		public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostViewModel)
		{
			var leftWalls = fumen.Lanes.Where(x => x.LaneType == LaneType.WallLeft).Select(x => new WallInfo
			{
				Wall = x,
				TGridRange = x.GetTGridRange(),
				XGridRange = x.GetXGridRange()
			}).OrderBy(x => x.TGridRange.Min).ToList();

			var rightWalls = fumen.Lanes.Where(x => x.LaneType == LaneType.WallRight).Select(x => new WallInfo
			{
				Wall = x,
				TGridRange = x.GetTGridRange(),
				XGridRange = x.GetXGridRange()
			}).OrderBy(x => x.TGridRange.Min).ToList();

			var allWalls = leftWalls.Concat(rightWalls).ToList();

			IEnumerable<ICheckResult> CheckTGridConfict(List<WallInfo> walls)
			{
				for (int i = 0; i < walls.Count - 1; i++)
				{
					var cur = walls[i];
					var next = walls[i + 1];

					if (cur.TGridRange.IsInRange(next.TGridRange, false) || next.TGridRange.IsInRange(cur.TGridRange, false))
					{
						yield return new CommonCheckResult()
						{
							RuleName = RuleName,
							Severity = RuleSeverity.Problem,
							Description = Resources.WallConflict.Format(cur.Wall.RecordId, next.Wall.RecordId),
							LocationDescription = cur.TGridRange.ToString(),
							NavigateBehavior = new NavigateToTGridBehavior(cur.Wall.TGrid)
						};
					}
				}
			}
			IEnumerable<ICheckResult> CheckWallChildrenTGridConfict(IEnumerable<WallInfo> walls)
			{
				foreach (var wall in walls)
				{
					var maxTGrid = wall.Wall.TGrid;

					foreach (var child in wall.Wall.Children)
					{
						if (child.TGrid < maxTGrid)
						{
							yield return new CommonCheckResult()
							{
								RuleName = RuleName,
								Severity = RuleSeverity.Error,
								LocationDescription = child.ToString(),
								Description = Resources.WallConflict3.Format(wall.Wall.RecordId, child.TGrid, maxTGrid),
								NavigateBehavior = new NavigateToObjectBehavior(child)
							};
							break;
						}

						maxTGrid = child.TGrid;
					}
				}
			}


			bool skipAfter = false;

			//检查一组墙是否存在时间冲突
			foreach (var result in CheckWallChildrenTGridConfict(allWalls))
			{
				yield return result;
				skipAfter = true;
			}

			if (skipAfter)
				yield break;
			skipAfter = false;

			//检查一组组墙之间是否时间冲突
			foreach (var result in CheckTGridConfict(leftWalls))
			{
				yield return result;
				skipAfter = true;
			}
			foreach (var result in CheckTGridConfict(rightWalls))
			{
				yield return result;
				skipAfter = true;
			}

			if (skipAfter)
				yield break;
			skipAfter = false;

			//检查左右墙之间是否会交叉
			foreach (var leftWall in leftWalls)
			{
				var leftTGridRange = leftWall.TGridRange;

				var inRangeRightWalls = rightWalls
					.SkipWhile(x => !x.TGridRange.IsInRange(leftTGridRange))
					.TakeWhile(x => x.TGridRange.IsInRange(leftTGridRange));

				foreach (var rightWall in inRangeRightWalls)
				{
					if (rightWall.XGridRange.IsInRange(leftWall.XGridRange, false))
					{
						var resT = leftWall.Wall.TGrid.ResT;
						var resX = leftWall.Wall.XGrid.ResX;

						var leftPoints =
							leftWall.Wall.Children.AsEnumerable<ConnectableObjectBase>().Prepend(leftWall.Wall)
							.Select(x => new Point() { x = x.XGrid.TotalGrid, y = x.TGrid.TotalGrid });

						var rightPoints =
							rightWall.Wall.Children.AsEnumerable<ConnectableObjectBase>().Prepend(rightWall.Wall)
							.Select(x => new Point() { x = x.XGrid.TotalGrid, y = x.TGrid.TotalGrid });

						var leftLines = leftPoints.IsOnlyOne() ? Enumerable.Empty<Line>() : leftPoints
							.SequenceConsecutivelyWrap(2)
							.Select(x => x.ToArray())
							.Select(x => new Line()
							{
								x1 = x[0].x,
								y1 = x[0].y,
								x2 = x[1].x,
								y2 = x[1].y
							}).ToArray();
						var rightLines = rightPoints.IsOnlyOne() ? Enumerable.Empty<Line>() : rightPoints
							.SequenceConsecutivelyWrap(2)
							.Select(x => x.ToArray())
							.Select(x => new Line()
							{
								x1 = x[0].x,
								y1 = x[0].y,
								x2 = x[1].x,
								y2 = x[1].y
							}).ToArray();

						(var leftLine, var rightLine, var point) = leftLines.SelectMany(x => rightLines.Select(y => (x, y, LineIntersection.FindIntersection(x, y, 0.000001f)))).FirstOrDefault(x => x.Item3 is not null);
						if (point is Point p)
						{
							var conflictXGrid = new XGrid((float)(p.x / resX), 0);
							conflictXGrid.NormalizeSelf();
							var conflictTGrid = new TGrid((float)(p.y / resT), 0);
							conflictTGrid.NormalizeSelf();

							yield return new CommonCheckResult()
							{
								RuleName = RuleName,
								Severity = RuleSeverity.Error,
								LocationDescription = $"leftLine:{leftLine} rightLine:{rightLine} conflict at {conflictXGrid} {conflictTGrid}",
								Description = Resources.WallConflict.Format(leftWall.Wall.RecordId, rightWall.Wall.RecordId),
								NavigateBehavior = new NavigateToTGridBehavior(conflictTGrid)
							};
						}
					}
				}
			}
		}
	}
}
