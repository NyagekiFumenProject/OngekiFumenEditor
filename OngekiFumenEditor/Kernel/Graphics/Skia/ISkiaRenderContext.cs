using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace OngekiFumenEditor.Kernel.Graphics.Skia
{
    public interface ISkiaRenderContext : IRenderContext
    {
        SKCanvas Canvas { get; }
    }
}
