using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    [Export(typeof(IFumenCheckRule))]
    internal class WallConflictCheckRule : IFumenCheckRule
    {
        public struct CheckResult : ICheckResult
        {
            public string RuleName => "WallConflict";

            public RuleSeverity Severity => RuleSeverity.Error;

            public string LocationDescription { get; set; }
            public string Description { get; set; }

            public TGrid NavigateTGridLocation { get; set; }

            public void Navigate(object fumenHostedObj)
            {
                if (fumenHostedObj is FumenVisualEditorViewModel editor)
                {
                    editor.ScrollTo(NavigateTGridLocation);
                }
            }
        }

        public struct WallInfo
        {
            public LaneStartBase Wall { get; set; }
            public GridRange TGridRange { get; set; }
            public GridRange XGridRange { get; set; }
        }

        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, object fumenHostViewModel)
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
                        yield return new CheckResult()
                        {
                            Description = $"墙(id:{cur.Wall.RecordId})与另一个同方向的墙(id:{next.Wall.RecordId})发生时间冲突",
                            LocationDescription = cur.TGridRange.ToString(),
                            NavigateTGridLocation = cur.Wall.TGrid
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
                            yield return new CheckResult()
                            {
                                LocationDescription = child.ToString(),
                                Description = $"墙(id:{wall.Wall.RecordId})Next/End物件的TGrid({child.TGrid})不能低于前者({maxTGrid})",
                                NavigateTGridLocation = child.TGrid
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
            foreach (var result in CheckTGridConfict(allWalls))
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
                        yield return new CheckResult()
                        {
                            LocationDescription = leftWall.Wall.TGrid.ToString(),
                            Description = $"不同边的墙(id:{leftWall.Wall.RecordId})和(id:{rightWall.Wall.RecordId})水平交叉碰撞或重合",
                            NavigateTGridLocation = leftWall.Wall.TGrid
                        };
                    }
                }
            }
        }
    }
}
