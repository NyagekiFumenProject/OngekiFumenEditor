using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.Lane;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using OpenTK.Graphics.OpenGL;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Numerics;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    internal class LaneBlockerDrawingTarget : CommonDrawTargetBase<OngekiTimelineObjectBase>
    {
        private readonly IPolygonDrawing polygonDrawing;
        private readonly HashSet<int> overdrawingDefferSet = new();

        public override IEnumerable<string> DrawTargetID { get; } = new[]
        {
            "LBK","[LBK_End]"
        };

        public override int DefaultRenderOrder => 80;

        public LaneBlockerDrawingTarget()
        {
            polygonDrawing = IoC.Get<IPolygonDrawing>();
        }

        public override void Begin(IFumenEditorDrawingContext target)
        {
            base.Begin(target);
            overdrawingDefferSet.Clear();
        }

        public override void Draw(IFumenEditorDrawingContext target, OngekiTimelineObjectBase obj)
        {
            var lbk = obj switch
            {
                LaneBlockArea l => l,
                LaneBlockArea.LaneBlockAreaEndIndicator e => e.RefLaneBlockArea,
                _ => null
            };

            if (lbk is null)
                return;

            var hashCode = lbk.GetHashCode();
            if (overdrawingDefferSet.Contains(hashCode))
                return;
            else
                overdrawingDefferSet.Add(hashCode);

            var fumen = target.Editor.Fumen;

            var offsetX = (lbk.Direction == LaneBlockArea.BlockDirection.Left ? -1 : 1) * 60;
            var color = lbk.Direction == LaneBlockArea.BlockDirection.Left ? WallLaneDrawTarget.LeftWallColor : WallLaneDrawTarget.RightWallColor;
            var colorF = color;
            colorF.W = -0.25f;
            (double, double) lastP = default;

            #region Generate LBK lines

            void PostPointByXTGrid(double xGridTotalUnit, double tGridTotalUnit, Vector4? specifyColor = default)
            {
                var x = (float)XGridCalculator.ConvertXGridToX(xGridTotalUnit, target.Editor);
                var y = (float)target.ConvertToY(tGridTotalUnit);

                //lineDrawing.PostPoint(new(x, y), specifyColor ?? color);
                polygonDrawing.PostPoint(new(x, y), Vector4.One);
                polygonDrawing.PostPoint(new(x + offsetX, y), colorF);

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
                polygonDrawing.Begin(target, PrimitiveType.TriangleStrip);
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
                polygonDrawing.End();
            }

            #endregion

            var itor = lbk
                .GetAffactableWallLanes(fumen)
                .Where(x => target.CheckRangeVisible(x.MinTGrid, x.MaxTGrid))
                .OrderBy(x => x.TGrid)
                .GetEnumerator();

            var beginTGrid = lbk.TGrid;
            var endTGrid = lbk.EndIndicator.TGrid;

            if (itor.MoveNext())
            {
                ProcessWallLane(itor.Current, beginTGrid, endTGrid);

                while (itor.MoveNext())
                {
                    var start = itor.Current;

                    (var prevTGrid, var prevXGrid) = lastP;
                    ProcessWallLane(start, beginTGrid, endTGrid);
                }
            }

            //lineDrawing.End();
        }
    }
}
