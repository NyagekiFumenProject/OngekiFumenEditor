using Advanced.Algorithms.Geometry;
using Caliburn.Micro;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Kernel.Graphics;
using System;
using System.Collections.Generic;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;
using static OngekiFumenEditor.Utils.MathUtils;
using System.Linq;
using OngekiFumenEditor.Utils;
using System.Numerics;
using OngekiFumenEditor.Utils.ObjectPool;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using EarcutNet;
using System.Drawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors
{
    public class DrawPlayableAreaHelper
    {
        private ILineDrawing lineDrawing;
        private IPolygonDrawing polygonDrawing;

        private Vector4 playFieldForegroundColor;
        private bool enablePlayFieldDrawing;

        LineVertex[] vertices = new LineVertex[2];

        public DrawPlayableAreaHelper()
        {
            lineDrawing = IoC.Get<ISimpleLineDrawing>();
            polygonDrawing = IoC.Get<IPolygonDrawing>();

            UpdateProps();
            Properties.EditorGlobalSetting.Default.PropertyChanged += Default_PropertyChanged;
        }

        private void Default_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Properties.EditorGlobalSetting.EnablePlayFieldDrawing):
                case nameof(Properties.EditorGlobalSetting.PlayFieldForegroundColor):
                    UpdateProps();
                    break;
                default:
                    break;
            }
        }

        private void UpdateProps()
        {
            enablePlayFieldDrawing = Properties.EditorGlobalSetting.Default.EnablePlayFieldDrawing;
            playFieldForegroundColor = Color.FromArgb(Properties.EditorGlobalSetting.Default.PlayFieldForegroundColor).ToVector4();
        }

        public void Draw(IFumenEditorDrawingContext target)
        {
            if (target.Editor.IsDesignMode)
                DrawAudioDuration(target);
        }

        private void DrawAudioDuration(IFumenEditorDrawingContext target)
        {
            var y = (float)target.Editor.TotalDurationHeight;

            var color = new Vector4(1, 0, 0, 1);
            vertices[0] = new(new(0, y), color, VertexDash.Solider);
            vertices[1] = new(new(target.ViewWidth, y), color, VertexDash.Solider);

            lineDrawing.Draw(target, vertices, 3);
        }

        public void DrawPlayField(IFumenEditorDrawingContext target, TGrid minTGrid, TGrid maxTGrid)
        {
            if (target.Editor.IsDesignMode || !enablePlayFieldDrawing)
                return;

            const long defaultLeftX = -24 * XGrid.DEFAULT_RES_X;
            const long defaultRightX = 24 * XGrid.DEFAULT_RES_X;

            var fumen = target.Editor.Fumen;

            void EnumeratePoints(bool isRight, List<Vector2> result)
            {
                var defaultX = isRight ? defaultRightX : defaultLeftX;
                var type = isRight ? LaneType.WallRight : LaneType.WallLeft;
                var ranges = CombinableRange<int>.CombineRanges(fumen.Lanes.GetVisibleStartObjects(minTGrid, maxTGrid)
                    .Where(x => x.LaneType == type)
                    .Select(x => new CombinableRange<int>(x.MinTGrid.TotalGrid, x.MaxTGrid.TotalGrid)))
                    .OrderBy(x => isRight ? x.Max : x.Min).ToArray();

                var points = new HashSet<float>();

                void appendPoint(List<Vector2> list, XGrid xGrid, float y)
                {
                    if (xGrid is null)
                        return;
                    list.Add(new(xGrid.TotalGrid, y));
                }

                for (int i = 0; i < ranges.Length; i++)
                {
                    var curRange = ranges[i];
                    var nextRange = ranges.ElementAtOrDefault(i + 1);

                    var lanes = fumen.Lanes
                        .GetVisibleStartObjects(TGrid.FromTotalGrid(curRange.Min), TGrid.FromTotalGrid(curRange.Max))
                        .Where(x => x.LaneType == type)
                        .ToArray();

                    var polylines = lanes.Select(x => x.GenAllPath().Where(x => minTGrid.TotalGrid <= x.pos.Y && x.pos.Y <= maxTGrid.TotalGrid).Select(x => x.pos).SequenceConsecutivelyWrap(2).Select(x => (x.FirstOrDefault(), x.LastOrDefault())).ToArray())
                        .ToArray();

                    for (int r = 0; r < polylines.Length; r++)
                    {
                        var polylineA = polylines[r];
                        for (int t = r + 1; t < polylines.Length; t++)
                        {
                            var polylineB = polylines[t];

                            for (int ai = 0; ai < polylineA.Length; ai++)
                            {
                                for (int bi = 0; bi < polylineB.Length; bi++)
                                {
                                    var a = polylineA[ai];
                                    var b = polylineB[bi];

                                    if (a == b)
                                        continue;

                                    var lineA = new Line(new(a.Item1.X, a.Item1.Y), new(a.Item2.X, a.Item2.Y));
                                    var lineB = new Line(new(b.Item1.X, b.Item1.Y), new(b.Item2.X, b.Item2.Y));

                                    var point = LineIntersection.Find(lineA, lineB);
                                    if (point is not null)
                                        points.Add((float)point.Y);
                                }
                            }
                        }
                    }

                    points.AddRange(lanes.Select(x => (float)x.TGrid.TotalGrid).Concat(lanes.Select(x => x.Children.LastOrDefault()).FilterNull().Select(x => (float)x.TGrid.TotalGrid)));
                }

                var sortedPoints = points.OrderBy(x => x).ToList();

                if (sortedPoints.IsEmpty() || sortedPoints.FirstOrDefault() > minTGrid.TotalGrid)
                    sortedPoints.Insert(0, minTGrid.TotalGrid);
                if (sortedPoints.LastOrDefault() < maxTGrid.TotalGrid)
                    sortedPoints.Add(maxTGrid.TotalGrid);

                var segments = sortedPoints.SequenceConsecutivelyWrap(2).Select(x => (x.FirstOrDefault(), x.LastOrDefault())).ToArray();

                foreach ((var fromY, var toY) in segments)
                {
                    var midY = ((fromY + toY) / 2);
                    var midTGrid = TGrid.FromTotalGrid((int)midY);

                    var pickables = fumen.Lanes
                            .GetVisibleStartObjects(midTGrid, midTGrid)
                            .Where(x => x.LaneType == type)
                            .Select(x => (x.CalulateXGrid(midTGrid), x))
                            .FilterNullBy(x => x.Item1)
                            .ToArray();

                    (var midXGrid, var pickLane) = pickables.IsEmpty() ? default : (isRight ? pickables.MaxBy(x => x.Item1) : pickables.MinBy(x => x.Item1));
                    if (pickLane is not null)
                    {
                        var fromTGrid = TGrid.FromTotalGrid((int)fromY);
                        appendPoint(result, pickLane.CalulateXGrid(fromTGrid), fromY);

                        foreach (var pos in pickLane.GenAllPath().Select(x => x.pos).SkipWhile(x => x.Y < fromY).TakeWhile(x => x.Y < toY))
                            result.Add(new(pos.X, pos.Y));

                        var toTGrid = TGrid.FromTotalGrid((int)toY);
                        appendPoint(result, pickLane.CalulateXGrid(toTGrid), toY);
                    }
                    else
                    {
                        //默认24咯
                        result.Add(new(defaultX, fromY));
                        result.Add(new(defaultX, toY));
                    }
                }
            }

            using var d3 = ObjectPool<List<double>>.GetWithUsingDisposable(out var points, out _);
            points.Clear();
            using var d4 = ObjectPool<List<int>>.GetWithUsingDisposable(out var idxList, out _);
            idxList.Clear();

            void FillPoints(List<Vector2> ps)
            {
                foreach (var point in ps.DistinctContinuousBy(x => x))
                {
                    var x = (float)XGridCalculator.ConvertXGridToX(point.X / XGrid.DEFAULT_RES_X, target.Editor);
                    var y = (float)target.ConvertToY(point.Y / TGrid.DEFAULT_RES_T);

                    points.Add(x);
                    points.Add(y);
                }
            }

            using var d = ObjectPool<List<Vector2>>.GetWithUsingDisposable(out var leftPoints, out _);
            leftPoints.Clear();
            using var d2 = ObjectPool<List<Vector2>>.GetWithUsingDisposable(out var rightPoints, out _);
            rightPoints.Clear();

            EnumeratePoints(false, leftPoints);
            FillPoints(leftPoints);
            EnumeratePoints(true, rightPoints);
            rightPoints.Reverse();
            FillPoints(rightPoints);
            /*
            foreach (var seq in points.SequenceWrap(2))
            {
                var x = XGrid.FromTotalGrid((int)seq.FirstOrDefault());
                var y = TGrid.FromTotalGrid((int)seq.LastOrDefault());


            }
            */
            var list = Earcut.Tessellate(points, idxList);

            polygonDrawing.Begin(target, OpenTK.Graphics.OpenGL.PrimitiveType.Triangles);

            //var random = new Random(1025);
            foreach (var seq in list.SequenceWrap(3))
            {
                //var playFieldForegroundColor = new Vector4((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble(), 1);
                foreach (var idx in seq)
                {
                    var x = (float)points[idx * 2 + 0];
                    var y = (float)points[idx * 2 + 1];
                    polygonDrawing.PostPoint(new(x, y), playFieldForegroundColor);
                }
            }

            polygonDrawing.End();
        }
    }
}
