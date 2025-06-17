using OngekiFumenEditor.Kernel.Graphics.Skia.Controls;
using SkiaSharp;
using SkiaSharp.Views.WPF;
using System;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL
{
    internal class DefaultSkiaRenderContext : IRenderContext
    {
        private DefaultSkiaDrawingManager manager;
        private readonly SkiaRenderControl renderControl;

        public event Action<TimeSpan> OnRender;

        public DefaultSkiaRenderContext(DefaultSkiaDrawingManager manager, SkiaRenderControl renderControl)
        {
            this.manager = manager;
            this.renderControl = renderControl;
        }

        public void AfterRender(IDrawingContext context)
        {

        }

        public void BeforeRender(IDrawingContext context)
        {

        }

        public void CleanRender(IDrawingContext context, Vector4 cleanColor)
        {

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
