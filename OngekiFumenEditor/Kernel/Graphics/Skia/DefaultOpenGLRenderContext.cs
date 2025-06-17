using SkiaSharp;
using System;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL
{
    internal class DefaultSkiaRenderContext : IRenderContext
    {
        private DefaultSkiaDrawingManager manager;
        private readonly SKCanvas canvas;

        public event Action<TimeSpan> OnRender;

        public DefaultSkiaRenderContext(DefaultSkiaDrawingManager manager, SKCanvas canvas)
        {
            this.manager = manager;
            this.canvas = canvas;
        }

        public void AfterRender(IDrawingContext context)
        {

        }

        public void BeforeRender(IDrawingContext context)
        {

        }

        public void CleanRender(IDrawingContext context, Vector4 cleanColor)
        {
            canvas.Clear(new SKColorF(cleanColor.X, cleanColor.Y, cleanColor.Z, cleanColor.W));
        }

        public void StartRendering()
        {
            throw new NotImplementedException();
        }

        public void StopRendering()
        {
            throw new NotImplementedException();
        }
    }
}
