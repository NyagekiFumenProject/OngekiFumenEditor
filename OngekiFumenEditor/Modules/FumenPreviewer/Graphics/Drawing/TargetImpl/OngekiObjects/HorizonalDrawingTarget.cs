using Caliburn.Micro;
using FontStashSharp;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl.Lane;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using OpenTK.Graphics.OpenGL;
using Polyline2DCSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using static OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl.OngekiObjects
{
    [Export(typeof(IDrawingTarget))]
    public class HorizonalDrawingTarget : CommonBatchDrawTargetBase<OngekiTimelineObjectBase>
    {
        public record RegisterDrawingInfo(OngekiTimelineObjectBase TimelineObject, double Y);

        private IStringDrawing stringDrawing;
        private ISimpleLineDrawing lineDrawing;
        private HashSet<int> overdrawingDefferSet = new HashSet<int>();

        public HorizonalDrawingTarget()
        {
            lineDrawing = IoC.Get<ISimpleLineDrawing>();
            stringDrawing = IoC.Get<IStringDrawing>();
        }

        public override IEnumerable<string> DrawTargetID { get; } = new[]
        {
            "MET","SFL","BPM","EST","CLK","LBK","[LBK_End]","[SFL_End]"
        };

        private static Dictionary<string, FSColor> colors = new()
        {
            {"MET", FSColor.LightGreen },
            {"SFL", FSColor.LightCyan },
            {"BPM", FSColor.Pink },
            {"EST", FSColor.Yellow },
            {"CLK", FSColor.CadetBlue },
            {"LBK", FSColor.HotPink },
            {"[LBK_End]", FSColor.HotPink },
            {"[SFL_End]", FSColor.LightCyan },
        };

        public override void DrawBatch(IFumenPreviewer target, IEnumerable<OngekiTimelineObjectBase> objs)
        {
            var fumen = target.Fumen;
            overdrawingDefferSet.Clear();
            using var d4 = objs.Select(x => new RegisterDrawingInfo(x, TGridCalculator.ConvertTGridToY(x.TGrid, fumen.BpmList, 1.0, 240))).ToListWithObjectPool(out var objects);

            foreach (var g in objects.GroupBy(x => x.TimelineObject.TGrid.TotalGrid))
            {
                var y = (float)g.FirstOrDefault().Y;
                using var d3 = g.ToListWithObjectPool(out var actualItems);
                if (y < target.CurrentPlayTime || y > target.CurrentPlayTime + target.ViewHeight)
                {
                    actualItems.RemoveAll(x => x.TimelineObject switch
                    {
                        LaneBlockArea or LaneBlockArea.LaneBlockAreaEndIndicator or Soflan or Soflan.SoflanEndIndicator => false,
                        _ => true
                    });
                    if (actualItems.Count == 0)
                        continue;
                }

                using var d = actualItems.Select(x => colors[x.TimelineObject.IDShortName]).OrderBy(x => x.PackedValue).ToListWithObjectPool(out var regColors);
                var per = 1.0f * target.ViewWidth / regColors.Count;
                lineDrawing.Begin(target, 2);
                for (int i = 0; i < regColors.Count; i++)
                {
                    var c = regColors[i];
                    lineDrawing.PostPoint(new(per * i, y), new(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, c.A / 255.0f));
                    lineDrawing.PostPoint(new(per * (i + 1), y), new(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, c.A / 255.0f));
                }
                lineDrawing.End();

                //draw range line if need
                foreach (var obj in actualItems)
                {
                    switch (obj.TimelineObject)
                    {
                        case LaneBlockArea.LaneBlockAreaEndIndicator laneBlockEnd:
                            DrawLaneBlockArea(target, laneBlockEnd.RefLaneBlockArea);
                            break;
                        case LaneBlockArea laneBlock:
                            DrawLaneBlockArea(target, laneBlock);
                            break;
                        default:
                            break;
                    }
                }

                DrawDescText(target, y, actualItems);
            }
        }

        private void DrawLaneBlockArea(IFumenPreviewer target, LaneBlockArea lbk)
        {
            var hashCode = lbk.GetHashCode();
            if (overdrawingDefferSet.Contains(hashCode))
                return;
            else
                overdrawingDefferSet.Add(hashCode);

            lineDrawing.Begin(target, 10);
            var fumen = target.Fumen;

            var offsetX = (lbk.Direction == LaneBlockArea.BlockDirection.Left ? -1 : 1) * 10;
            var color = lbk.Direction == LaneBlockArea.BlockDirection.Left ? WallLaneDrawTarget.LeftWallColor : WallLaneDrawTarget.RightWallColor;
            (double, double) lastP = default;

            #region Generate LBK lines

            void PostPointByXTGrid(double xGridTotalUnit, double tGridTotalUnit, Vector4? specifyColor = default)
            {
                var x = (float)XGridCalculator.ConvertXGridToX(xGridTotalUnit, 30, target.ViewWidth, 1) + offsetX;
                var y = (float)TGridCalculator.ConvertTGridUnitToY(tGridTotalUnit, fumen.BpmList, 1.0, 240);

                lineDrawing.PostPoint(new(x, y), specifyColor ?? color);
                lastP = (tGridTotalUnit, xGridTotalUnit);
            }

            void PostPointByTGrid(ConnectableChildObjectBase obj, TGrid grid, Vector4? specifyColor = default)
            {
                var xGridTotalGridOpt = obj.CalulateXGridTotalGrid(grid.TotalGrid);
                if (xGridTotalGridOpt is double xGridTotalGrid)
                    PostPointByXTGrid(xGridTotalGrid / XGrid.DEFAULT_RES_X, grid.TotalUnit, specifyColor);
            }

            void ProcessConnectable(ConnectableChildObjectBase obj, TGrid minTGrid, TGrid maxTGrid)
            {
                var minTotalGrid = minTGrid.TotalGrid;
                var maxTotalGrid = maxTGrid.TotalGrid;

                if (!obj.IsCurvePath)
                {
                    //直线，优化
                    PostPointByTGrid(obj, minTGrid);
                    PostPointByTGrid(obj, maxTGrid);
                }
                else
                {
                    using var d = ObjectPool<List<Vector2>>.GetWithUsingDisposable(out var list, out _);
                    list.Clear();

                    foreach ((var gridVec2, var isVaild) in obj.GetConnectionPaths().Where(x => x.pos.Y <= maxTotalGrid && x.pos.Y >= minTotalGrid))
                    {
                        if (!isVaild)
                        {
                            PostPointByXTGrid(obj.PrevObject.XGrid.TotalUnit, minTGrid.TotalUnit);
                            PostPointByXTGrid(obj.XGrid.TotalUnit, maxTGrid.TotalUnit);
                            return;
                        }
                        list.Add(new(gridVec2.X, gridVec2.Y));
                    }
                    foreach (var gridVec2 in list)
                        PostPointByXTGrid(gridVec2.X / obj.XGrid.ResX, gridVec2.Y / obj.TGrid.ResT);
                }
            }

            void ProcessWallLane(LaneStartBase wallStartLane, TGrid minTGrid, TGrid maxTGrid)
            {
                foreach (var child in wallStartLane.Children)
                {
                    if (child.TGrid < minTGrid)
                        continue;
                    if (child.PrevObject.TGrid > maxTGrid)
                        break;

                    var childMinTGrid = MathUtils.Max(minTGrid, child.PrevObject.TGrid);
                    var childMaxTGrid = MathUtils.Min(maxTGrid, child.TGrid);

                    ProcessConnectable(child, childMinTGrid, childMaxTGrid);
                }
            }

            #endregion

            var itor = lbk.GetAffactableWallLanes(fumen).OrderBy(x => x.TGrid).GetEnumerator();

            var beginTGrid = lbk.TGrid;
            var endTGrid = lbk.EndIndicator.TGrid;

            if (itor.MoveNext())
            {
                ProcessWallLane(itor.Current, beginTGrid, endTGrid);

                while (itor.MoveNext())
                {
                    var start = itor.Current;

                    (var prevTGrid, var prevXGrid) = lastP;
                    PostPointByXTGrid(prevXGrid, prevTGrid, Vector4.Zero);
                    PostPointByXTGrid(start.XGrid.TotalUnit, start.TGrid.TotalUnit, Vector4.Zero);
                    PostPointByXTGrid(start.XGrid.TotalUnit, start.TGrid.TotalUnit);

                    ProcessWallLane(start, beginTGrid, endTGrid);
                }
            }

            lineDrawing.End();
        }

        private void DrawDescText(IFumenPreviewer target, float y, IEnumerable<RegisterDrawingInfo> group)
        {
            string formatObj(OngekiObjectBase s) => s switch
            {
                BPMChange o => $"BPM:{(int)o.BPM}",
                MeterChange o => $"MET:{o.Bunbo}/{o.BunShi}",
                Soflan o => $"SPD:{(int)o.Speed:F2}x",
                Soflan.SoflanEndIndicator o => $"{formatObj(o.RefSoflan)}_End",
                LaneBlockArea o => $"LBK:{o.Direction}",
                LaneBlockArea.LaneBlockAreaEndIndicator o => $"{formatObj(o.RefLaneBlockArea)}_End",
                EnemySet o => $"EST:{o.TagTblValue}",
                ClickSE o => $"CLK",
                _ => string.Empty
            };

            var x = 0f;
            var i = 0;
            foreach ((var obj, var c) in group.Select(x => (x.TimelineObject, colors[x.TimelineObject.IDShortName])).OrderBy(x => x.Item2.PackedValue))
            {
                stringDrawing.Draw((i == 0 ? string.Empty : " / ") + formatObj(obj), new Vector2(x, y + 12), Vector2.One, 16, 0, new(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, c.A / 255.0f), new(0, 0.5f), IStringDrawing.StringStyle.Normal, target, default, out var size);
                x += size.Value.X;
                i++;
            }
        }
    }
}
