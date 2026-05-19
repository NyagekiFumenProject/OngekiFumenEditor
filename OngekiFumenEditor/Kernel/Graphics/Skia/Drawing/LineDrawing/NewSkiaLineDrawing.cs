using SkiaSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.LineDrawing
{
    internal sealed class NewSkiaLineDrawing : CommonSkiaDrawingBase, ILineDrawing, ISimpleLineDrawing, IDisposable
    {
        private const int MeshGradientSegmentThreshold = 256;
        private const float MeshAntialiasRadius = 1.5f;

        private readonly Dictionary<VertexDash, SKPathEffect> dashPathEffectCache = new();
        private readonly List<LineVertex> postedPoints = new();
        private readonly Stack<SKPath> pathPool = new();
        private readonly SKPaint strokePaint = new()
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };
        private readonly SKPaint meshPaint = new()
        {
            IsAntialias = true,
            Color = SKColors.White
        };

        private SKPoint[] meshPointsBuffer = Array.Empty<SKPoint>();
        private SKColor[] meshColorsBuffer = Array.Empty<SKColor>();
        private SKPoint[] meshDrawPointsBuffer = Array.Empty<SKPoint>();
        private SKColor[] meshDrawColorsBuffer = Array.Empty<SKColor>();

        private SKCanvas canvas;
        private IDrawingContext target;
        private float postedLineWidth;

        public NewSkiaLineDrawing(DefaultSkiaDrawingManagerImpl manager) : base(manager)
        {
        }

        public void Begin(IDrawingContext target, float lineWidth)
        {
            BeginSession(target, lineWidth);
            postedPoints.Clear();
        }

        public void PostPoint(Vector2 point, Vector4 color, VertexDash dash)
        {
            PostPoint(new LineVertex(point, color, dash));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PostPoint(LineVertex vertex)
        {
            postedPoints.Add(vertex);
        }

        public void End()
        {
            try
            {
                var span = CollectionsMarshal.AsSpan(postedPoints);
                PrepareAndDraw(span, postedLineWidth);
            }
            finally
            {
                EndSession();
                postedPoints.Clear();
            }
        }

        public void Draw(IDrawingContext target, IEnumerable<LineVertex> points, float lineWidth)
        {
            Begin(target, lineWidth);
            foreach (var p in points)
                PostPoint(p);
            End();
        }

        public IStaticVBODrawing.IVBOHandle GenerateVBOWithPresetPoints(IEnumerable<LineVertex> points, float lineWidth)
        {
            return new SkiaLineVBOHandle(points, lineWidth);
        }

        public void DrawVBO(IDrawingContext target, IStaticVBODrawing.IVBOHandle vbo)
        {
            if (vbo is not SkiaLineVBOHandle handle || handle.Points.Length == 0)
                return;

            BeginSession(target, handle.LineWidth);
            try
            {
                PrepareAndDraw(handle.Points.AsSpan(), handle.LineWidth);
            }
            finally
            {
                EndSession();
            }
        }

        private void BeginSession(IDrawingContext target, float lineWidth)
        {
            OnBegin(target);

            this.target = target;
            postedLineWidth = lineWidth;
            canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;
        }

        private void EndSession()
        {
            strokePaint.PathEffect = null;
            strokePaint.Shader = null;

            OnEnd();

            target = default;
            canvas = default;
            postedLineWidth = default;
        }

        private void PrepareAndDraw(ReadOnlySpan<LineVertex> points, float lineWidth)
        {
            if (points.Length < 2 || !(lineWidth > 0))
                return;

            var maxSegments = points.Length - 1;
            var segmentsBuffer = ArrayPool<LineSegment>.Shared.Rent(maxSegments);
            var runsBuffer = ArrayPool<Run>.Shared.Rent(maxSegments);

            try
            {
                BuildSegmentsAndRuns(
                    points,
                    segmentsBuffer,
                    runsBuffer,
                    out var segmentCount,
                    out var runCount,
                    out var gradientCount);

                if (segmentCount == 0)
                    return;

                var segmentsSpan = new ReadOnlySpan<LineSegment>(segmentsBuffer, 0, segmentCount);
                var runsSpan = new ReadOnlySpan<Run>(runsBuffer, 0, runCount);

                if (gradientCount > MeshGradientSegmentThreshold)
                {
                    DrawMeshFallback(segmentsSpan, lineWidth);
                    return;
                }

                for (var i = 0; i < runCount; i++)
                    DrawRun(runsSpan[i], segmentsSpan, lineWidth);
            }
            finally
            {
                ArrayPool<LineSegment>.Shared.Return(segmentsBuffer, clearArray: true);
                ArrayPool<Run>.Shared.Return(runsBuffer, clearArray: true);
            }
        }

        private static void BuildSegmentsAndRuns(
            ReadOnlySpan<LineVertex> points,
            LineSegment[] segmentsBuffer,
            Run[] runsBuffer,
            out int segmentCount,
            out int runCount,
            out int gradientCount)
        {
            segmentCount = 0;
            runCount = 0;
            gradientCount = 0;

            Run currentRun = default;
            var hasCurrent = false;

            for (var i = 0; i < points.Length - 1; i++)
            {
                if (!LineSegment.TryCreate(points[i], points[i + 1], out var segment))
                    continue;

                segmentsBuffer[segmentCount] = segment;
                var segmentIndex = segmentCount;
                segmentCount++;

                if (segment.IsGradient)
                    gradientCount++;

                var dash = segment.Dash;
                var color = segment.StartColor;
                var isGradient = segment.IsGradient;

                if (hasCurrent
                    && !isGradient
                    && !currentRun.HasGradient
                    && currentRun.Color == color
                    && currentRun.Dash == dash)
                {
                    var isContinuous = currentRun.IsContinuousPolyline
                        && segmentsBuffer[currentRun.StartIndex + currentRun.Count - 1].EndPoint == segment.StartPoint;
                    currentRun = currentRun with
                    {
                        Count = currentRun.Count + 1,
                        IsContinuousPolyline = isContinuous
                    };
                    runsBuffer[runCount - 1] = currentRun;
                    continue;
                }

                currentRun = new Run(
                    StartIndex: segmentIndex,
                    Count: 1,
                    Color: color,
                    Dash: dash,
                    HasGradient: isGradient,
                    IsContinuousPolyline: !isGradient);

                runsBuffer[runCount] = currentRun;
                runCount++;
                hasCurrent = true;

                if (isGradient)
                    hasCurrent = false;
            }
        }

        private void DrawRun(Run run, ReadOnlySpan<LineSegment> segments, float lineWidth)
        {
            var runSegments = segments.Slice(run.StartIndex, run.Count);

            if (run.HasGradient)
            {
                DrawGradientRun(runSegments, lineWidth);
                return;
            }

            DrawPolylineOrSegmentsRun(runSegments, run.Color, run.Dash, run.IsContinuousPolyline, IsDashed(run.Dash), lineWidth);
        }

        private void DrawGradientRun(ReadOnlySpan<LineSegment> segments, float lineWidth)
        {
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                var path = RentPath();
                try
                {
                    path.MoveTo(ToSKPoint(segment.StartPoint));
                    path.LineTo(ToSKPoint(segment.EndPoint));

                    using var shader = CreateGradientShader(segment);
                    strokePaint.StrokeWidth = lineWidth;
                    strokePaint.PathEffect = GetDashPathEffect(segment.Dash);
                    strokePaint.Shader = shader;
                    strokePaint.Color = SKColors.White;

                    canvas.DrawPath(path, strokePaint);
                    target.PerfomenceMonitor.CountDrawCall(this);
                }
                finally
                {
                    strokePaint.Shader = null;
                    ReturnPath(path);
                }
            }
        }

        private void DrawPolylineOrSegmentsRun(
            ReadOnlySpan<LineSegment> segments,
            Vector4 color,
            VertexDash dash,
            bool isContinuousPolyline,
            bool dashed,
            float lineWidth)
        {
            var path = RentPath();
            try
            {
                if (isContinuousPolyline && !dashed)
                {
                    path.MoveTo(ToSKPoint(segments[0].StartPoint));
                    for (var i = 0; i < segments.Length; i++)
                        path.LineTo(ToSKPoint(segments[i].EndPoint));
                }
                else
                {
                    var prevEnd = new Vector2(float.NaN, float.NaN);
                    for (var i = 0; i < segments.Length; i++)
                    {
                        var segment = segments[i];
                        if (dashed || segment.StartPoint != prevEnd)
                            path.MoveTo(ToSKPoint(segment.StartPoint));
                        path.LineTo(ToSKPoint(segment.EndPoint));
                        prevEnd = segment.EndPoint;
                    }
                }

                strokePaint.StrokeWidth = lineWidth;
                strokePaint.PathEffect = GetDashPathEffect(dash);
                strokePaint.Shader = null;
                strokePaint.Color = ToSKColor(color);

                canvas.DrawPath(path, strokePaint);
                target.PerfomenceMonitor.CountDrawCall(this);
            }
            finally
            {
                ReturnPath(path);
            }
        }

        private void DrawMeshFallback(ReadOnlySpan<LineSegment> segments, float lineWidth)
        {
            var maxVerts = EstimateMeshVertexCount(segments);
            if (maxVerts == 0)
                return;

            EnsureMeshCapacity(maxVerts);
            Array.Clear(meshPointsBuffer);
            Array.Clear(meshColorsBuffer);
            var pointsBuffer = meshPointsBuffer;
            var colorsBuffer = meshColorsBuffer;

            var written = 0;
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (IsDashed(segment.Dash))
                    AddDashedSegmentMesh(pointsBuffer, colorsBuffer, ref written, segment, lineWidth);
                else
                    AddSegmentMesh(pointsBuffer, colorsBuffer, ref written, segment.StartPoint, segment.EndPoint, segment.StartColor, segment.EndColor, lineWidth);
            }

            if (written == 0)
                return;

            EnsureMeshDrawBufferEnoughSize(written);

            Array.Clear(meshDrawPointsBuffer);
            Array.Clear(meshDrawColorsBuffer);

            Array.Copy(pointsBuffer, meshDrawPointsBuffer, written);
            Array.Copy(colorsBuffer, meshDrawColorsBuffer, written);

            canvas.DrawVertices(SKVertexMode.Triangles, meshDrawPointsBuffer, meshDrawColorsBuffer, meshPaint);
            target.PerfomenceMonitor.CountDrawCall(this);
        }

        private void EnsureMeshCapacity(int required)
        {
            if (meshPointsBuffer.Length < required)
            {
                meshPointsBuffer = new SKPoint[required];
                meshColorsBuffer = new SKColor[required];
            }
        }

        private void EnsureMeshDrawBufferEnoughSize(int requireSize)
        {
            if (meshDrawPointsBuffer.Length < requireSize)
            {
                meshDrawPointsBuffer = new SKPoint[requireSize];
                meshDrawColorsBuffer = new SKColor[requireSize];
            }
        }

        private static int EstimateMeshVertexCount(ReadOnlySpan<LineSegment> segments)
        {
            var count = 0;
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (IsDashed(segment.Dash))
                {
                    var cycleLength = (float)(segment.Dash.DashSize + segment.Dash.GapSize);
                    if (!(cycleLength > 0))
                        count += 18;
                    else
                        count += (int)Math.Ceiling(segment.Length / cycleLength) * 18 + 18;
                }
                else
                {
                    count += 18;
                }
            }
            return count;
        }

        private SKPath RentPath()
        {
            if (pathPool.Count > 0)
                return pathPool.Pop();
            return new SKPath();
        }

        private void ReturnPath(SKPath path)
        {
            path.Reset();
            pathPool.Push(path);
        }

        private SKPathEffect GetDashPathEffect(VertexDash dash)
        {
            if (!IsDashed(dash))
                return null;

            if (!dashPathEffectCache.TryGetValue(dash, out var pathEffect))
            {
                pathEffect = SKPathEffect.CreateDash(new[] { (float)dash.DashSize, (float)dash.GapSize }, 0);
                dashPathEffectCache[dash] = pathEffect;
            }

            return pathEffect;
        }

        public void Dispose()
        {
            strokePaint.PathEffect = null;
            strokePaint.Shader = null;
            strokePaint.Dispose();
            meshPaint.Dispose();

            while (pathPool.Count > 0)
                pathPool.Pop().Dispose();

            foreach (var pathEffect in dashPathEffectCache.Values)
                pathEffect?.Dispose();

            dashPathEffectCache.Clear();
        }

        private sealed class SkiaLineVBOHandle : IStaticVBODrawing.IVBOHandle
        {
            public LineVertex[] Points { get; private set; }
            public float LineWidth { get; private set; }

            public SkiaLineVBOHandle(IEnumerable<LineVertex> points, float lineWidth)
            {
                Points = points?.ToArray() ?? [];
                LineWidth = lineWidth;
            }

            public void Dispose()
            {
                Points = [];
                LineWidth = 0;
            }
        }

        private readonly record struct Run(
            int StartIndex,
            int Count,
            Vector4 Color,
            VertexDash Dash,
            bool HasGradient,
            bool IsContinuousPolyline);

        private readonly record struct LineSegment(
            Vector2 StartPoint,
            Vector2 EndPoint,
            Vector4 StartColor,
            Vector4 EndColor,
            VertexDash Dash,
            float Length)
        {
            public bool IsGradient => StartColor != EndColor;

            public static bool TryCreate(LineVertex start, LineVertex end, out LineSegment segment)
            {
                segment = default;

                if (start is null || end is null)
                    return false;

                var length = Vector2.Distance(start.Point, end.Point);
                if (!(length > 0))
                    return false;

                segment = new LineSegment(start.Point, end.Point, start.Color, end.Color, NormalizeDash(start.Dash), length);
                return true;
            }
        }

        private static void AddDashedSegmentMesh(SKPoint[] points, SKColor[] colors, ref int written, LineSegment segment, float lineWidth)
        {
            var dashLength = (float)segment.Dash.DashSize;
            var cycleLength = dashLength + segment.Dash.GapSize;

            if (!(dashLength > 0) || !(cycleLength > 0))
            {
                AddSegmentMesh(points, colors, ref written, segment.StartPoint, segment.EndPoint, segment.StartColor, segment.EndColor, lineWidth);
                return;
            }

            for (var startDistance = 0f; startDistance < segment.Length; startDistance += cycleLength)
            {
                var endDistance = MathF.Min(startDistance + dashLength, segment.Length);
                if (!(endDistance > startDistance))
                    continue;

                var startT = startDistance / segment.Length;
                var endT = endDistance / segment.Length;

                AddSegmentMesh(
                    points,
                    colors,
                    ref written,
                    Vector2.Lerp(segment.StartPoint, segment.EndPoint, startT),
                    Vector2.Lerp(segment.StartPoint, segment.EndPoint, endT),
                    Vector4.Lerp(segment.StartColor, segment.EndColor, startT),
                    Vector4.Lerp(segment.StartColor, segment.EndColor, endT),
                    lineWidth);
            }
        }

        private static void AddSegmentMesh(
            SKPoint[] points,
            SKColor[] colors,
            ref int written,
            Vector2 startPoint,
            Vector2 endPoint,
            Vector4 startColor,
            Vector4 endColor,
            float lineWidth)
        {
            var delta = endPoint - startPoint;
            var length = delta.Length();
            if (!(length > 0))
                return;

            var normal = new Vector2(-delta.Y, delta.X) / length;
            var halfWidth = lineWidth * 0.5f;

            var startInnerA = startPoint - normal * halfWidth;
            var startInnerB = startPoint + normal * halfWidth;
            var endInnerA = endPoint - normal * halfWidth;
            var endInnerB = endPoint + normal * halfWidth;
            var startOuterA = startPoint - normal * (halfWidth + MeshAntialiasRadius);
            var startOuterB = startPoint + normal * (halfWidth + MeshAntialiasRadius);
            var endOuterA = endPoint - normal * (halfWidth + MeshAntialiasRadius);
            var endOuterB = endPoint + normal * (halfWidth + MeshAntialiasRadius);

            var transparentStartColor = WithAlpha(startColor, 0);
            var transparentEndColor = WithAlpha(endColor, 0);

            AddQuad(points, colors, ref written, startOuterA, endOuterA, endInnerA, startInnerA, transparentStartColor, transparentEndColor, endColor, startColor);
            AddQuad(points, colors, ref written, startInnerA, endInnerA, endInnerB, startInnerB, startColor, endColor, endColor, startColor);
            AddQuad(points, colors, ref written, startInnerB, endInnerB, endOuterB, startOuterB, startColor, endColor, transparentEndColor, transparentStartColor);
        }

        private static void AddQuad(
            SKPoint[] points,
            SKColor[] colors,
            ref int written,
            Vector2 p0,
            Vector2 p1,
            Vector2 p2,
            Vector2 p3,
            Vector4 c0,
            Vector4 c1,
            Vector4 c2,
            Vector4 c3)
        {
            AddVertex(points, colors, ref written, p0, c0);
            AddVertex(points, colors, ref written, p1, c1);
            AddVertex(points, colors, ref written, p2, c2);
            AddVertex(points, colors, ref written, p0, c0);
            AddVertex(points, colors, ref written, p2, c2);
            AddVertex(points, colors, ref written, p3, c3);
        }

        private static void AddVertex(SKPoint[] points, SKColor[] colors, ref int written, Vector2 point, Vector4 color)
        {
            points[written] = ToSKPoint(point);
            colors[written] = ToSKColor(color);
            written++;
        }

        private static SKShader CreateGradientShader(LineSegment segment)
        {
            return SKShader.CreateLinearGradient(
                ToSKPoint(segment.StartPoint),
                ToSKPoint(segment.EndPoint),
                new[] { ToSKColor(segment.StartColor), ToSKColor(segment.EndColor) },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp);
        }

        private static VertexDash NormalizeDash(VertexDash dash)
        {
            return IsDashed(dash) ? dash : VertexDash.Solider;
        }

        private static bool IsDashed(VertexDash dash)
        {
            return dash is not null && dash.DashSize > 0 && dash.GapSize > 0;
        }

        private static SKPoint ToSKPoint(Vector2 point)
        {
            return new SKPoint(point.X, point.Y);
        }

        private static SKColor ToSKColor(Vector4 color)
        {
            return new SKColor(
                ToColorComponent(color.X),
                ToColorComponent(color.Y),
                ToColorComponent(color.Z),
                ToColorComponent(color.W));
        }

        private static byte ToColorComponent(float value)
        {
            return (byte)(Math.Clamp(value, 0f, 1f) * 255);
        }

        private static Vector4 WithAlpha(Vector4 color, float alpha)
        {
            return new Vector4(color.X, color.Y, color.Z, alpha);
        }
    }
}
