using NetTopologySuite.GeometriesGraph;
using OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using SkiaSharp;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;
using static OngekiFumenEditor.Kernel.Graphics.IStaticVBODrawing;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.CircleDrawing
{
    internal class DefaultSkiaSimpleLineDrawing : CommonSkiaDrawingBase, ISimpleLineDrawing
    {
        private float lineWidth;
        private SKCanvas canvas;
        private IDrawingContext target;

        public DefaultSkiaSimpleLineDrawing(DefaultSkiaDrawingManager manager) : base(manager)
        {

        }

        public void Begin(IDrawingContext target, float lineWidth)
        {
            OnBegin(target);

            this.target = target;
            this.lineWidth = lineWidth;
            canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;
            prevPaintParam = default;
            prevPoint = null;
        }

        public void End()
        {
            OnEnd();

            target = default;
            canvas = default;
            prevPaintParam = default;
            prevPaint?.Dispose();
        }

        private (Vector4 color, VertexDash dash, float lineWidth) prevPaintParam = default;
        private SKPaint prevPaint = default;

        private SKPaint GetPaint(Vector4 color, VertexDash dash, float lineWidth)
        {
            var param = (color, dash, lineWidth);
            if (param == prevPaintParam && prevPaint != null)
                return prevPaint;

            prevPaint?.Dispose();

            var skColor = new SKColor(
                (byte)(color.X * 255),
                (byte)(color.Y * 255),
                (byte)(color.Z * 255),
                (byte)(color.W * 255));

            var paint = new SKPaint
            {
                Color = skColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = lineWidth,
                PathEffect = dash == VertexDash.Solider ? null : SKPathEffect.CreateDash([10, 5], 0)
            };

            prevPaint = paint;
            prevPaintParam = param;

            return paint;
        }

        public void Draw(IDrawingContext target, IEnumerable<LineVertex> points, float lineWidth)
        {
            Begin(target, lineWidth);

            foreach (var item in points)
                PostPoint(item.Point, item.Color, item.Dash);

            End();
        }

        private Vector2? prevPoint = null;

        public void PostPoint(Vector2 point, Vector4 color, VertexDash dash)
        {
            var paint = GetPaint(color, dash, lineWidth);
            if (prevPoint is Vector2 prevP)
            {
                canvas.DrawLine(prevP.X, prevP.Y, point.X, point.Y, paint);
                target.PerfomenceMonitor.CountDrawCall(this);
            }
            prevPoint = point;
        }

        private class SkiaVerticesWrapper : IVBOHandle
        {
            private List<(SKPath, SKPaint)> drawcalls;

            public IEnumerable<(SKPath, SKPaint)> DrawCalls => drawcalls;

            public SkiaVerticesWrapper(List<(SKPath, SKPaint)> drawcalls)
            {
                this.drawcalls = drawcalls;
            }

            public void Dispose()
            {
                foreach (var (path, paint) in drawcalls)
                {
                    path?.Dispose();
                    paint?.Dispose();
                }
            }
        }

        public IVBOHandle GenerateVBOWithPresetPoints(IEnumerable<LineVertex> p, float lineWidth)
        {
            throw new System.NotImplementedException();
        }

        public void DrawVBO(IDrawingContext target, IStaticVBODrawing.IVBOHandle vbo)
        {
            throw new System.NotImplementedException();
        }
    }
}
