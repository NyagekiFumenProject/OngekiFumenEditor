using OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums;
using OngekiFumenEditor.Kernel.Graphics.Skia.Base;
using OngekiFumenEditor.Utils;
using OpenTK.Mathematics;
using SkiaSharp;
using System;
using System.Windows.Media.Imaging;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.BeamDrawing
{
    internal class DefaultSkiaBeamDrawing : CommonSkiaDrawingBase, IBeamDrawing
    {
        private SKCanvas canvas;
        private IDrawingContext target;

        public DefaultSkiaBeamDrawing(DefaultSkiaDrawingManager manager) : base(manager)
        {

        }

        private void Begin(IDrawingContext target)
        {
            OnBegin(target);
        }

        private void End()
        {
            OnEnd();
        }

        public void Draw(IDrawingContext target, IImage tex, int width, float x, float progress, Vector4 color, float rotate, float judgeOffset)
        {
            Begin(target);

            var texture = (SkiaImage)tex;
            var canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;

            var alpha = MathUtils.SmoothStep(-1, 0, progress) * (1 - MathUtils.SmoothStep(1, 2, progress));

            canvas.Save();

            canvas.ResetMatrix();
            canvas.Translate(x - width / 2, 0);
            canvas.RotateDegrees(rotate);
            canvas.Scale(width * 1.0f / texture.Width, (target.CurrentDrawingTargetContext.Rect.Height + 40) * 2f / texture.Height);

            using var paint = new SKPaint();
            paint.Color = new SKColor((byte)(255 * color.X), (byte)(255 * color.Y), (byte)(255 * color.Z), (byte)(255 * alpha * color.W));

            var rect = SKRect.Create(0,
                0,
                texture.Width,
                texture.Height);
            var rect2 = SKRect.Create(0,
                0,
                texture.Width,
                target.CurrentDrawingTargetContext.Rect.Height);

            canvas.DrawImage(texture.Image, rect, rect2, paint);
            target.PerfomenceMonitor.CountDrawCall(this);

            canvas.Restore();

            End();
        }
    }
}
