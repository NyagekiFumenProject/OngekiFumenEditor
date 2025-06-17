using System;
using System.Numerics;
using System.Windows;

namespace OngekiFumenEditor.Kernel.Graphics
{
    public interface IRenderContext
    {
        public event Action<TimeSpan> OnRender;

        void BeforeRender(IDrawingContext context);
        void AfterRender(IDrawingContext context);
        void CleanRender(IDrawingContext context, Vector4 cleanColor);
        void StartRendering();
        void StopRendering();
    }
}
