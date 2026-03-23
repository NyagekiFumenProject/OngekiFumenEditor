using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Core.Base.OngekiObjects;
using OngekiFumenEditor.Core.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Core.Properties;
using OngekiFumenEditor.Core.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Core.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    internal class WallConflictCheckRule : IFumenCheckRule
    {
        private const string RuleName = "WallConflict";

        private struct WallInfo
        {
            public LaneStartBase Wall { get; set; }
            public GridRange TGridRange { get; set; }
            public GridRange XGridRange { get; set; }
        }

        private struct Line
        {
            public double X1 { get; set; }
            public double Y1 { get; set; }
            public double X2 { get; set; }
            public double Y2 { get; set; }

            public override readonly string ToString() => $"({X1:F2},{Y1:F2})-({X2:F2},{Y2:F2})";
        }

        private struct Point
        {
            public double X { get; set; }
            public double Y { get; set; }
        }

        private static class LineIntersection
        {
            public static Point? FindIntersection(Line lineA, Line lineB, double tolerance = 0.001)
            {
                double x1 = lineA.X1, y1 = lineA.Y1;
                double x2 = lineA.X2, y2 = lineA.Y2;
                double x3 = lineB.X1, y3 = lineB.Y1;
                double x4 = lineB.X2, y4 = lineB.Y2;

                if (Math.Abs(x1 - x2) < tolerance && Math.Abs(x3 - x4) < tolerance && Math.Abs(x1 - x3) < tolerance)
                    return new Point { X = x1, Y = y3 };

                if (Math.Abs(y1 - y2) < tolerance && Math.Abs(y3 - y4) < tolerance && Math.Abs(y1 - y3) < tolerance)
                    return new Point { X = x1, Y = y3 };

                if ((Math.Abs(x1 - x2) < tolerance && Math.Abs(x3 - x4) < tolerance) ||
                    (Math.Abs(y1 - y2) < tolerance && Math.Abs(y3 - y4) < tolerance))
                    return null;

                double x;
                double y;

                if (Math.Abs(x1 - x2) < tolerance)
                {
                    var m2 = (y4 - y3) / (x4 - x3);
                    var c2 = -m2 * x3 + y3;
                    x = x1;
                    y = c2 + m2 * x1;
                }
                else if (Math.Abs(x3 - x4) < tolerance)
                {
                    var m1 = (y2 - y1) / (x2 - x1);
                    var c1 = -m1 * x1 + y1;
                    x = x3;
                    y = c1 + m1 * x3;
                }
                else
                {
                    var m1 = (y2 - y1) / (x2 - x1);
                    var c1 = -m1 * x1 + y1;
                    var m2 = (y4 - y3) / (x4 - x3);
                    var c2 = -m2 * x3 + y3;

                    x = (c1 - c2) / (m2 - m1);
                    y = c2 + m2 * x;

                    if (!(Math.Abs(-m1 * x + y - c1) < tolerance && Math.Abs(-m2 * x + y - c2) < tolerance))
                        return null;
                }

                return IsInsideLine(lineA, x, y) && IsInsideLine(lineB, x, y) ? new Point { X = x, Y = y } : null;
            }

            private static bool IsInsideLine(Line line, double x, double y)
            {
                return (x >= line.X1 && x <= line.X2 || x >= line.X2 && x <= line.X1) &&
                       (y >= line.Y1 && y <= line.Y2 || y >= line.Y2 && y <= line.Y1);
            }
        }

        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
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

            foreach (var result in CheckWallChildrenTGridConflict(allWalls))
                yield return result;

            foreach (var result in CheckTGridConflict(leftWalls))
                yield return result;

            foreach (var result in CheckTGridConflict(rightWalls))
                yield return result;

            foreach (var result in CheckLeftRightWallIntersection(leftWalls, rightWalls))
                yield return result;
        }

        private static IEnumerable<ICheckResult> CheckTGridConflict(List<WallInfo> walls)
        {
            for (var i = 0; i < walls.Count - 1; i++)
            {
                var cur = walls[i];
                var next = walls[i + 1];

                if (cur.TGridRange.IsInRange(next.TGridRange, false) || next.TGridRange.IsInRange(cur.TGridRange, false))
                {
                    yield return new CommonCheckResult
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

        private static IEnumerable<ICheckResult> CheckWallChildrenTGridConflict(IEnumerable<WallInfo> walls)
        {
            foreach (var wall in walls)
            {
                var maxTGrid = wall.Wall.TGrid;

                foreach (var child in wall.Wall.Children)
                {
                    if (child.TGrid < maxTGrid)
                    {
                        yield return new CommonCheckResult
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

        private static IEnumerable<ICheckResult> CheckLeftRightWallIntersection(List<WallInfo> leftWalls, List<WallInfo> rightWalls)
        {
            foreach (var leftWall in leftWalls)
            {
                var leftTGridRange = leftWall.TGridRange;
                var inRangeRightWalls = rightWalls
                    .SkipWhile(x => !x.TGridRange.IsInRange(leftTGridRange))
                    .TakeWhile(x => x.TGridRange.IsInRange(leftTGridRange));

                foreach (var rightWall in inRangeRightWalls)
                {
                    if (!rightWall.XGridRange.IsInRange(leftWall.XGridRange, false))
                        continue;

                    var resT = leftWall.Wall.TGrid.ResT;
                    var resX = leftWall.Wall.XGrid.ResX;

                    var leftPoints = leftWall.Wall.Children.AsEnumerable<ConnectableObjectBase>().Prepend(leftWall.Wall)
                        .Select(x => new Point { X = x.XGrid.TotalGrid, Y = x.TGrid.TotalGrid });
                    var rightPoints = rightWall.Wall.Children.AsEnumerable<ConnectableObjectBase>().Prepend(rightWall.Wall)
                        .Select(x => new Point { X = x.XGrid.TotalGrid, Y = x.TGrid.TotalGrid });

                    var leftLines = leftPoints.IsOnlyOne()
                        ? Enumerable.Empty<Line>()
                        : leftPoints.SequenceConsecutivelyWrap(2).Select(x => x.ToArray()).Select(x => new Line
                        {
                            X1 = x[0].X, Y1 = x[0].Y, X2 = x[1].X, Y2 = x[1].Y
                        }).ToArray();

                    var rightLines = rightPoints.IsOnlyOne()
                        ? Enumerable.Empty<Line>()
                        : rightPoints.SequenceConsecutivelyWrap(2).Select(x => x.ToArray()).Select(x => new Line
                        {
                            X1 = x[0].X, Y1 = x[0].Y, X2 = x[1].X, Y2 = x[1].Y
                        }).ToArray();

                    var intersection = leftLines
                        .SelectMany(x => rightLines.Select(y => (leftLine: x, rightLine: y, point: LineIntersection.FindIntersection(x, y, 0.000001f))))
                        .FirstOrDefault(x => x.point is not null);

                    if (intersection.point is Point point)
                    {
                        var conflictXGrid = new XGrid((float)(point.X / resX), 0);
                        conflictXGrid.NormalizeSelf();
                        var conflictTGrid = new TGrid((float)(point.Y / resT), 0);
                        conflictTGrid.NormalizeSelf();

                        yield return new CommonCheckResult
                        {
                            RuleName = RuleName,
                            Severity = RuleSeverity.Error,
                            LocationDescription = $"leftLine:{intersection.leftLine} rightLine:{intersection.rightLine} conflict at {conflictXGrid} {conflictTGrid}",
                            Description = Resources.WallConflict.Format(leftWall.Wall.RecordId, rightWall.Wall.RecordId),
                            NavigateBehavior = new NavigateToTGridBehavior(conflictTGrid)
                        };
                    }
                }
            }
        }
    }
}

