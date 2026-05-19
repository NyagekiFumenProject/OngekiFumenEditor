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
        private const int MaxPooledPaths = 32;
        private const int StackPolyThreshold = 64;
        private const int StackSegmentThreshold = 32;

        private readonly Dictionary<VertexDash, SKPathEffect> dashPathEffectCache = new();
        private readonly List<LineVertex> postedPoints = new(1024);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

            if (maxSegments <= StackSegmentThreshold)
            {
                Span<LineSegment> segmentsBuf = stackalloc LineSegment[StackSegmentThreshold];
                Span<Run> runsBuf = stackalloc Run[StackSegmentThreshold];
                PrepareAndDrawCore(points, segmentsBuf, runsBuf, lineWidth);
                return;
            }

            var segmentsBuffer = ArrayPool<LineSegment>.Shared.Rent(maxSegments);
            var runsBuffer = ArrayPool<Run>.Shared.Rent(maxSegments);

            try
            {
                PrepareAndDrawCore(points, segmentsBuffer, runsBuffer, lineWidth);
            }
            finally
            {
                ArrayPool<LineSegment>.Shared.Return(segmentsBuffer, clearArray: false);
                ArrayPool<Run>.Shared.Return(runsBuffer, clearArray: false);
            }
        }

        private void PrepareAndDrawCore(
            ReadOnlySpan<LineVertex> points,
            Span<LineSegment> segmentsBuffer,
            Span<Run> runsBuffer,
            float lineWidth)
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

            ReadOnlySpan<LineSegment> segmentsSpan = segmentsBuffer[..segmentCount];
            ReadOnlySpan<Run> runsSpan = runsBuffer[..runCount];

            if (gradientCount > MeshGradientSegmentThreshold)
            {
                DrawMeshFallback(segmentsSpan, lineWidth);
                return;
            }

            for (var i = 0; i < runCount; i++)
                DrawRun(runsSpan[i], segmentsSpan, lineWidth);
        }

        private static void BuildSegmentsAndRuns(
            ReadOnlySpan<LineVertex> points,
            Span<LineSegment> segmentsBuffer,
            Span<Run> runsBuffer,
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
                    ref var runRef = ref runsBuffer[runCount - 1];
                    runRef.IsContinuousPolyline = runRef.IsContinuousPolyline
                        && segmentsBuffer[runRef.StartIndex + runRef.Count - 1].EndPoint == segment.StartPoint;
                    runRef.Count += 1;
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
                    strokePaint.PathEffect = GetOrCreateCachedDashPathEffect(segment.Dash);
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
                    BuildContinuousPolyline(path, segments);
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
                strokePaint.PathEffect = GetOrCreateCachedDashPathEffect(dash);
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

        private static void BuildContinuousPolyline(SKPath path, ReadOnlySpan<LineSegment> segments)
        {
            var pointCount = segments.Length + 1;

            if (pointCount <= StackPolyThreshold)
            {
                Span<SKPoint> buf = stackalloc SKPoint[StackPolyThreshold];
                var slice = buf[..pointCount];
                slice[0] = ToSKPoint(segments[0].StartPoint);
                for (var i = 0; i < segments.Length; i++)
                    slice[i + 1] = ToSKPoint(segments[i].EndPoint);
                path.AddPoly(slice, close: false);
            }
            else
            {
                var rented = ArrayPool<SKPoint>.Shared.Rent(pointCount);
                try
                {
                    var slice = rented.AsSpan(0, pointCount);
                    slice[0] = ToSKPoint(segments[0].StartPoint);
                    for (var i = 0; i < segments.Length; i++)
                        slice[i + 1] = ToSKPoint(segments[i].EndPoint);
                    path.AddPoly(slice, close: false);
                }
                finally
                {
                    ArrayPool<SKPoint>.Shared.Return(rented, clearArray: false);
                }
            }
        }

        private void DrawMeshFallback(ReadOnlySpan<LineSegment> segments, float lineWidth)
        {
            var maxVerts = EstimateMeshVertexCount(segments);
            if (maxVerts == 0)
                return;

            EnsureMeshCapacityAndClearTails(maxVerts);

            var written = 0;
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (IsDashed(segment.Dash))
                    AddDashedSegmentMesh(meshPointsBuffer, meshColorsBuffer, ref written, segment, lineWidth);
                else
                    AddSegmentMesh(meshPointsBuffer, meshColorsBuffer, ref written, segment.StartPoint, segment.EndPoint, segment.StartColor, segment.EndColor, segment.Length, lineWidth);
            }

            if (written == 0)
                return;

            canvas.DrawVertices(SKVertexMode.Triangles, meshPointsBuffer, meshColorsBuffer, meshPaint);
            target.PerfomenceMonitor.CountDrawCall(this);
        }

        private void EnsureMeshCapacityAndClearTails(int required)
        {
            if (meshPointsBuffer.Length < required)
            {
                meshPointsBuffer = new SKPoint[required];
                meshColorsBuffer = new SKColor[required];
                return;
            }

            var tailLength = meshPointsBuffer.Length - required;
            if (tailLength > 0)
            {
                Array.Clear(meshPointsBuffer, required, tailLength);
                Array.Clear(meshColorsBuffer, required, tailLength);
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
            if (pathPool.Count >= MaxPooledPaths)
            {
                path.Dispose();
                return;
            }
            pathPool.Push(path);
        }

        private SKPathEffect GetOrCreateCachedDashPathEffect(VertexDash dash)
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

        private record struct Run(
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
                AddSegmentMesh(points, colors, ref written, segment.StartPoint, segment.EndPoint, segment.StartColor, segment.EndColor, segment.Length, lineWidth);
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
                    (endT - startT) * segment.Length,
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
            float length,
            float lineWidth)
        {
            if (!(length > 0))
                return;

            var delta = endPoint - startPoint;
            var normal = new Vector2(-delta.Y, delta.X) / length;
            var halfWidth = lineWidth * 0.5f;

            var inner = normal * halfWidth;
            var outer = normal * (halfWidth + MeshAntialiasRadius);

            var skStartInnerA = ToSKPoint(startPoint - inner);
            var skStartInnerB = ToSKPoint(startPoint + inner);
            var skEndInnerA = ToSKPoint(endPoint - inner);
            var skEndInnerB = ToSKPoint(endPoint + inner);
            var skStartOuterA = ToSKPoint(startPoint - outer);
            var skStartOuterB = ToSKPoint(startPoint + outer);
            var skEndOuterA = ToSKPoint(endPoint - outer);
            var skEndOuterB = ToSKPoint(endPoint + outer);

            var skStart = ToSKColor(startColor);
            var skEnd = ToSKColor(endColor);
            var skTransparentStart = skStart.WithAlpha(0);
            var skTransparentEnd = skEnd.WithAlpha(0);

            AddQuad(points, colors, ref written, skStartOuterA, skEndOuterA, skEndInnerA, skStartInnerA, skTransparentStart, skTransparentEnd, skEnd, skStart);
            AddQuad(points, colors, ref written, skStartInnerA, skEndInnerA, skEndInnerB, skStartInnerB, skStart, skEnd, skEnd, skStart);
            AddQuad(points, colors, ref written, skStartInnerB, skEndInnerB, skEndOuterB, skStartOuterB, skStart, skEnd, skTransparentEnd, skTransparentStart);
        }

        private static void AddQuad(
            SKPoint[] points,
            SKColor[] colors,
            ref int written,
            SKPoint p0,
            SKPoint p1,
            SKPoint p2,
            SKPoint p3,
            SKColor c0,
            SKColor c1,
            SKColor c2,
            SKColor c3)
        {
            AddVertex(points, colors, ref written, p0, c0);
            AddVertex(points, colors, ref written, p1, c1);
            AddVertex(points, colors, ref written, p2, c2);
            AddVertex(points, colors, ref written, p0, c0);
            AddVertex(points, colors, ref written, p2, c2);
            AddVertex(points, colors, ref written, p3, c3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddVertex(SKPoint[] points, SKColor[] colors, ref int written, SKPoint point, SKColor color)
        {
            points[written] = point;
            colors[written] = color;
            written++;
        }

        private static SKShader CreateGradientShader(LineSegment segment)
        {
            return SKShader.CreateLinearGradient(
                ToSKPoint(segment.StartPoint),
                ToSKPoint(segment.EndPoint),
                [ToSKColor(segment.StartColor), ToSKColor(segment.EndColor)],
                [0f, 1f],
                SKShaderTileMode.Clamp);
        }

        private static VertexDash NormalizeDash(VertexDash dash)
        {
            return IsDashed(dash) ? dash : VertexDash.Solider;
        }

        private static bool IsDashed(VertexDash dash)
        {
            return dash.DashSize > 0 && dash.GapSize > 0;
        }

        private static SKPoint ToSKPoint(Vector2 point)
        {
            return new SKPoint(point.X, point.Y);
        }

        private static SKColor ToSKColor(Vector4 color)
        {
            return new SKColor(
                (byte)(color.X * 255),
                (byte)(color.Y * 255),
                (byte)(color.Z * 255),
                (byte)(color.W * 255));
        }
    }
}
