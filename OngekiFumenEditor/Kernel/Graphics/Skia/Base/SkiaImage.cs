using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.Base
{
    public class SkiaImage : IImage
    {
        public SkiaImage(SKImage image)
        {
            Image = image;
        }

        public SKImage Image { get; private set; }

        public int Width => Image.Width;
        public int Height => Image.Height;

        public TextureWrapMode TextureWrapT { get; set; } = TextureWrapMode.Clamp;
        public TextureWrapMode TextureWrapS { get; set; } = TextureWrapMode.Clamp;

        public void Dispose()
        {
            Image?.Dispose();
            Image = default;
        }
    }
}
