using OngekiFumenEditor.Kernel.Graphics.OpenGL;
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Base;
using OngekiFumenEditor.Kernel.Graphics.Skia.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Utils;
using OpenTK.Graphics.OpenGL;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Numerics;
using Vector2 = System.Numerics.Vector2;
using Vector3 = OpenTK.Mathematics.Vector3;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.TextureDrawing
{
    internal class DefaultSkiaTextureDrawing : CommonSkiaDrawingBase, ITextureDrawing, IDisposable
    {
        public DefaultSkiaTextureDrawing(DefaultSkiaDrawingManager manager) : base(manager)
        {

        }

        public void Dispose()
        {

        }

        public void Draw(IDrawingContext target, IImage texture, IEnumerable<(Vector2 size, Vector2 position, float rotation, Vector4 color)> instances)
        {
            foreach ((var size, var position, var rotation, var color) in instances)
                Draw(target, texture as SkiaImage, size, position, rotation, color);
        }

        private void Draw(IDrawingContext target, SkiaImage tex, Vector2 size, Vector2 position, float rotation, Vector4 color)
        {
            OnBegin(target);
            var canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;

            var adjustSize = new Vector2(Math.Abs(size.X), Math.Abs(size.Y));

            canvas.Save();

            var adjustPosition = position.ToSkiaSharpPoint();

            canvas.Translate(adjustPosition.X, adjustPosition.Y);
            canvas.RotateDegrees(rotation);
            canvas.Scale(Math.Sign(size.X), -1 * Math.Sign(size.Y));
            var rect = SKRect.Create(-adjustSize.X / 2,
                -adjustSize.Y / 2,
                adjustSize.X,
                adjustSize.Y);

            using var paint = new SKPaint();

            canvas.DrawImage(tex.Image, rect, paint);
            target.PerfomenceMonitor.CountDrawCall(this);

            canvas.Restore();

            OnEnd();
        }
    }
}
