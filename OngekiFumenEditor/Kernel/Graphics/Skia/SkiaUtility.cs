using System;
using System.Diagnostics;

namespace OngekiFumenEditor.Kernel.Graphics.Skia
{
    internal static class SkiaUtility
    {
        [Conditional("DEBUG")]
        public static void CheckSkiaRenderContext(IRenderContext renderContext)
        {
            if (renderContext is not ISkiaRenderContext)
                throw new InvalidOperationException("Render context must implement ISkiaRenderContext.");
        }
    }
}
