using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.LineDrawing
{
    internal sealed class NewSkiaLineDrawing : CommonSkiaDrawingBase, ILineDrawing, ISimpleLineDrawing, IDisposable
    {
        private const int MeshGradientSegmentThreshold = 256;
        private const int PathDrawCallThreshold = 512;
        private const float MeshAntialiasRadius = 1.5f;

        private readonly Dictionary<VertexDash, SKPathEffect> dashPathEffectCache = new();
        private readonly List<LineVertex> postedPoints = new();
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
                using var data = PreparedLineData.Create(postedPoints, postedLineWidth);
                DrawPreparedData(data);
            }
            finally
            {
                EndSession();
                postedPoints.Clear();
            }
        }

        public void Draw(IDrawingContext target, IEnumerable<LineVertex> points, float lineWidth)
        {
            using var data = PreparedLineData.Create(points, lineWidth);
            if (!data.HasSegments)
                return;

            BeginSession(target, data.LineWidth);
            try
            {
                DrawPreparedData(data);
            }
            finally
            {
                EndSession();
            }
        }

        public IStaticVBODrawing.IVBOHandle GenerateVBOWithPresetPoints(IEnumerable<LineVertex> points, float lineWidth)
        {
            return new SkiaLineVBOHandle(points, lineWidth);
        }

        public void DrawVBO(IDrawingContext target, IStaticVBODrawing.IVBOHandle vbo)
        {
            if (vbo is not SkiaLineVBOHandle handle || !handle.Data.HasSegments)
                return;

            BeginSession(target, handle.Data.LineWidth);
            try
            {
                DrawPreparedData(handle.Data);
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

        private void DrawPreparedData(PreparedLineData data)
        {
            if (!data.HasSegments)
                return;

            if (data.UseMesh)
                DrawMesh(data.MeshPoints, data.MeshColors);
            else
                DrawPathBatches(data.PathBatches, data.LineWidth);
        }

        private void DrawPathBatches(IReadOnlyList<PathBatch> batches, float lineWidth)
        {
            foreach (var batch in batches)
            {
                strokePaint.StrokeWidth = lineWidth;
                strokePaint.PathEffect = GetDashPathEffect(batch.Dash);
                strokePaint.Shader = batch.Shader;
                strokePaint.Color = batch.Shader is null ? ToSKColor(batch.Color) : SKColors.White;

                canvas.DrawPath(batch.Path, strokePaint);
                target.PerfomenceMonitor.CountDrawCall(this);
            }

            strokePaint.Shader = null;
        }

        private void DrawMesh(SKPoint[] points, SKColor[] colors)
        {
            if (points.Length == 0)
                return;

            canvas.DrawVertices(SKVertexMode.Triangles, points, colors, meshPaint);
            target.PerfomenceMonitor.CountDrawCall(this);
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

            foreach (var pathEffect in dashPathEffectCache.Values)
                pathEffect?.Dispose();

            dashPathEffectCache.Clear();
        }

        private sealed class SkiaLineVBOHandle : IStaticVBODrawing.IVBOHandle
        {
            public PreparedLineData Data { get; private set; }

            public SkiaLineVBOHandle(IEnumerable<LineVertex> points, float lineWidth)
            {
                Data = PreparedLineData.Create(points, lineWidth);
            }

            public void Dispose()
            {
                Data?.Dispose();
                Data = PreparedLineData.Empty;
            }
        }

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

        private sealed class PathBatch : IDisposable
        {
            public SKPath Path { get; } = new();
            public Vector2 EndPoint { get; private set; }
            public Vector4 Color { get; }
            public VertexDash Dash { get; }
            public SKShader Shader { get; }

            public PathBatch(LineSegment segment)
            {
                Path.MoveTo(ToSKPoint(segment.StartPoint));
                Path.LineTo(ToSKPoint(segment.EndPoint));

                EndPoint = segment.EndPoint;
                Color = segment.StartColor;
                Dash = segment.Dash;
                Shader = segment.IsGradient ? CreateGradientShader(segment) : null;
            }

            public bool CanAppend(LineSegment segment)
            {
                return Shader is null
                    && !segment.IsGradient
                    && EndPoint == segment.StartPoint
                    && Color == segment.StartColor
                    && Dash == segment.Dash;
            }

            public void Append(LineSegment segment)
            {
                if (IsDashed(Dash))
                    Path.MoveTo(ToSKPoint(segment.StartPoint));

                Path.LineTo(ToSKPoint(segment.EndPoint));
                EndPoint = segment.EndPoint;
            }

            public void Dispose()
            {
                Path.Dispose();
                Shader?.Dispose();
            }
        }

        private sealed class PreparedLineData : IDisposable
        {
            public static PreparedLineData Empty { get; } = new([], 0, [], 0, 0, false, [], [], []);

            public LineVertex[] Points { get; }
            public float LineWidth { get; }
            public LineSegment[] Segments { get; }
            public int GradientSegmentCount { get; }
            public int EstimatedPathDrawCalls { get; }
            public bool UseMesh { get; }
            public PathBatch[] PathBatches { get; }
            public SKPoint[] MeshPoints { get; }
            public SKColor[] MeshColors { get; }
            public bool HasSegments => Segments.Length > 0;

            private PreparedLineData(
                LineVertex[] points,
                float lineWidth,
                LineSegment[] segments,
                int gradientSegmentCount,
                int estimatedPathDrawCalls,
                bool useMesh,
                PathBatch[] pathBatches,
                SKPoint[] meshPoints,
                SKColor[] meshColors)
            {
                Points = points;
                LineWidth = lineWidth;
                Segments = segments;
                GradientSegmentCount = gradientSegmentCount;
                EstimatedPathDrawCalls = estimatedPathDrawCalls;
                UseMesh = useMesh;
                PathBatches = pathBatches;
                MeshPoints = meshPoints;
                MeshColors = meshColors;
            }

            public static PreparedLineData Create(IEnumerable<LineVertex> points, float lineWidth)
            {
                var sourcePoints = points?.ToArray() ?? [];
                var segments = BuildSegments(sourcePoints, lineWidth);
                if (segments.Length == 0)
                    return new PreparedLineData(sourcePoints, lineWidth, [], 0, 0, false, [], [], []);

                var (gradientCount, estimatedDrawCalls) = AnalyzePathPlan(segments);
                var useMesh = gradientCount > MeshGradientSegmentThreshold || estimatedDrawCalls > PathDrawCallThreshold;

                if (useMesh)
                {
                    var (meshPoints, meshColors) = BuildMesh(segments, lineWidth);
                    return new PreparedLineData(sourcePoints, lineWidth, segments, gradientCount, estimatedDrawCalls, true, [], meshPoints, meshColors);
                }

                return new PreparedLineData(sourcePoints, lineWidth, segments, gradientCount, estimatedDrawCalls, false, BuildPathBatches(segments), [], []);
            }

            public void Dispose()
            {
                if (ReferenceEquals(this, Empty))
                    return;

                foreach (var batch in PathBatches)
                    batch.Dispose();
            }
        }

        private static LineSegment[] BuildSegments(IReadOnlyList<LineVertex> points, float lineWidth)
        {
            if (points.Count < 2 || !(lineWidth > 0))
                return [];

            var segments = new List<LineSegment>(points.Count - 1);

            for (var i = 0; i < points.Count - 1; i++)
                if (LineSegment.TryCreate(points[i], points[i + 1], out var segment))
                    segments.Add(segment);

            return segments.ToArray();
        }

        private static (int GradientCount, int DrawCalls) AnalyzePathPlan(IReadOnlyList<LineSegment> segments)
        {
            var gradientCount = 0;
            var drawCalls = 0;
            PathPlanBatch currentBatch = default;

            foreach (var segment in segments)
            {
                if (segment.IsGradient)
                {
                    gradientCount++;
                    drawCalls++;
                    currentBatch = default;
                    continue;
                }

                if (currentBatch.CanAppend(segment))
                {
                    currentBatch = currentBatch.Append(segment);
                    continue;
                }

                drawCalls++;
                currentBatch = PathPlanBatch.Create(segment);
            }

            return (gradientCount, drawCalls);
        }

        private readonly record struct PathPlanBatch(Vector2 EndPoint, Vector4 Color, VertexDash Dash, bool HasValue)
        {
            public static PathPlanBatch Create(LineSegment segment) => new(segment.EndPoint, segment.StartColor, segment.Dash, true);

            public bool CanAppend(LineSegment segment)
            {
                return HasValue
                    && EndPoint == segment.StartPoint
                    && Color == segment.StartColor
                    && Dash == segment.Dash;
            }

            public PathPlanBatch Append(LineSegment segment) => this with { EndPoint = segment.EndPoint };
        }

        private static PathBatch[] BuildPathBatches(IReadOnlyList<LineSegment> segments)
        {
            var batches = new List<PathBatch>();
            PathBatch currentBatch = default;

            foreach (var segment in segments)
            {
                if (currentBatch?.CanAppend(segment) == true)
                {
                    currentBatch.Append(segment);
                    continue;
                }

                currentBatch = new PathBatch(segment);
                batches.Add(currentBatch);

                if (segment.IsGradient)
                    currentBatch = default;
            }

            return batches.ToArray();
        }

        private static (SKPoint[] Points, SKColor[] Colors) BuildMesh(IReadOnlyList<LineSegment> segments, float lineWidth)
        {
            var points = new List<SKPoint>(segments.Count * 18);
            var colors = new List<SKColor>(segments.Count * 18);

            foreach (var segment in segments)
                if (IsDashed(segment.Dash))
                    AddDashedSegmentMesh(points, colors, segment, lineWidth);
                else
                    AddSegmentMesh(points, colors, segment.StartPoint, segment.EndPoint, segment.StartColor, segment.EndColor, lineWidth);

            return (points.ToArray(), colors.ToArray());
        }

        private static void AddDashedSegmentMesh(List<SKPoint> points, List<SKColor> colors, LineSegment segment, float lineWidth)
        {
            var dashLength = (float)segment.Dash.DashSize;
            var cycleLength = dashLength + segment.Dash.GapSize;

            if (!(dashLength > 0) || !(cycleLength > 0))
            {
                AddSegmentMesh(points, colors, segment.StartPoint, segment.EndPoint, segment.StartColor, segment.EndColor, lineWidth);
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
                    Vector2.Lerp(segment.StartPoint, segment.EndPoint, startT),
                    Vector2.Lerp(segment.StartPoint, segment.EndPoint, endT),
                    Vector4.Lerp(segment.StartColor, segment.EndColor, startT),
                    Vector4.Lerp(segment.StartColor, segment.EndColor, endT),
                    lineWidth);
            }
        }

        private static void AddSegmentMesh(
            List<SKPoint> points,
            List<SKColor> colors,
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

            AddQuad(points, colors, startOuterA, endOuterA, endInnerA, startInnerA, transparentStartColor, transparentEndColor, endColor, startColor);
            AddQuad(points, colors, startInnerA, endInnerA, endInnerB, startInnerB, startColor, endColor, endColor, startColor);
            AddQuad(points, colors, startInnerB, endInnerB, endOuterB, startOuterB, startColor, endColor, transparentEndColor, transparentStartColor);
        }

        private static void AddQuad(
            List<SKPoint> points,
            List<SKColor> colors,
            Vector2 p0,
            Vector2 p1,
            Vector2 p2,
            Vector2 p3,
            Vector4 c0,
            Vector4 c1,
            Vector4 c2,
            Vector4 c3)
        {
            AddVertex(points, colors, p0, c0);
            AddVertex(points, colors, p1, c1);
            AddVertex(points, colors, p2, c2);
            AddVertex(points, colors, p0, c0);
            AddVertex(points, colors, p2, c2);
            AddVertex(points, colors, p3, c3);
        }

        private static void AddVertex(List<SKPoint> points, List<SKColor> colors, Vector2 point, Vector4 color)
        {
            points.Add(ToSKPoint(point));
            colors.Add(ToSKColor(color));
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
