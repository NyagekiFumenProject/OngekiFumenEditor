using OngekiFumenEditor.Utils;
using SkiaSharp;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.PolygonDrawing
{
    internal class DefaultSkiaPolygonDrawing : CommonSkiaDrawingBase, IPolygonDrawing
    {
        private IDrawingContext target;
        private Primitive primitive;
        private SKCanvas canvas;

        private List<SKPoint> points = new();
        private List<SKColor> colors = new();


        public DefaultSkiaPolygonDrawing(DefaultSkiaDrawingManagerImpl manager) : base(manager)
        {

        }

        public void Begin(IDrawingContext target, Primitive primitive)
        {
            OnBegin(target);

            this.primitive = primitive;
            canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;
            points.Clear();
            colors.Clear();
            this.target = target;
        }

        public void End()
        {
            //draw
            using var paint = new SKPaint()
            {
                IsAntialias = true,
                Color = SKColors.White
            };

            canvas.DrawVertices(primitive switch
            {
                Primitive.Triangles => SKVertexMode.Triangles,
                Primitive.TriangleStrip => SKVertexMode.TriangleStrip,
            }, points.ToArray(), colors.ToArray(), paint);
            target.PerfomenceMonitor.CountDrawCall(this);

            //clean
            OnEnd();
            canvas = default;
            target = default;
        }

        public void PostPoint(Vector2 Point, Vector4 Color)
        {
            colors.Add(new SKColor(
                (byte)(Color.X * 255),
                (byte)(Color.Y * 255),
                (byte)(Color.Z * 255),
                (byte)(Color.W * 255)));
            points.Add(Point);
        }
    }
}
