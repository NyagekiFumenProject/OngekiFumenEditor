using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Base;
using OngekiFumenEditor.Avalonia.Utils;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Numerics;
using Vector2 = System.Numerics.Vector2;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.TextureDrawing
{
    internal class DefaultSkiaTextureDrawing : CommonSkiaDrawingBase, ITextureDrawing, IDisposable
    {
        public DefaultSkiaTextureDrawing(DefaultSkiaDrawingManagerImpl manager) : base(manager)
        {

        }

        public void Dispose()
        {

        }

        public void Draw(IDrawingContext target, IImage texture, IEnumerable<(Vector2 size, Vector2 position, float rotation, Vector4 color)> instances)
        {
            var tex = texture as SkiaImage;
            if (tex?.Image is null)
                return;

            // Artist setup (canvas save + MVP composition) and the paint are shared across
            // all sprites of this command, matching DefaultSkiaBatchTextureDrawing. This only
            // creates one SKPaint instead of one per instance and avoids re-composing the MVP
            // matrix for every sprite.
            OnBegin(target);
            try
            {
                var canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;

                using var paint = new SKPaint();
                foreach ((var size, var position, var rotation, var color) in instances)
                {
                    paint.Color = color.ToSKColor();

                    var adjustSize = new Vector2(Math.Abs(size.X), Math.Abs(size.Y));

                    canvas.Save();

                    var adjustPosition = position.ToSkiaSharpPoint();

                    canvas.Translate(adjustPosition.X, adjustPosition.Y);
                    canvas.RotateRadians(rotation);
                    canvas.Scale(Math.Sign(size.X), -1 * Math.Sign(size.Y));
                    var rect = SKRect.Create(-adjustSize.X / 2,
                        -adjustSize.Y / 2,
                        adjustSize.X,
                        adjustSize.Y);

                    canvas.DrawImage(tex.Image, rect, paint);
                    target.PerfomenceMonitor.CountDrawCall();

                    canvas.Restore();
                }
            }
            finally
            {
                OnEnd();
            }
        }
    }
}


