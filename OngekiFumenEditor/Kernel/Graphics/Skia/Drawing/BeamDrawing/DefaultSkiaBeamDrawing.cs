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

        void CreateColorTintMatrix(Span<float> arr, Vector4 tintColor, float strength)
        {
            // 颜色归一化
            float r = tintColor.X;
            float g = tintColor.Y;
            float b = tintColor.Z;
            float a = tintColor.W;

            float i = 1 - strength;

            Span<float> ree = [
                i + strength * r, strength * r,     strength * r,     0, 0,
                strength * g,     i + strength * g, strength * g,     0, 0,
                strength * b,     strength * b,     i + strength * b, 0, 0,
                0,                0,                0,                a, 0,
            ];

            ree.CopyTo(arr);
        }

        void CreateSolidColorMatrix(Span<float> arr, Vector4 color)
        {
            float r = color.X;
            float g = color.Y;
            float b = color.Z;
            float a = color.W;

            Span<float> ree = [
                r, 0, 0, 0, 0,
                0, g, 0, 0, 0,
                0, 0, b, 0, 0,
                0, 0, 0, a, 0,
            ];

            ree.CopyTo(arr);
        }

        public void Draw(IDrawingContext target, IImage tex, int width, float x, float progress, Vector4 color, float rotate, float judgeOffset)
        {
            Begin(target);

            var texture = (SkiaImage)tex;
            var canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;
            var height = target.CurrentDrawingTargetContext.Rect.Height;

            var alpha = MathUtils.SmoothStep(-1, 0, progress) * (1 - MathUtils.SmoothStep(1, 2, progress));
            var actualWidth = MathUtils.SmoothStep(-1, 0, progress) * (1 - MathUtils.SmoothStep(1, 2f, progress)) * width;

            canvas.Save();

            var angle = MathUtils.RadianToAngle(rotate);

            var fixedColor = color;
            fixedColor.W *= alpha;

            using var paint = new SKPaint();
            Span<float> colorMatrix = stackalloc float[20];
            CreateSolidColorMatrix(colorMatrix, color);
            paint.ColorFilter = SKColorFilter.CreateColorMatrix(colorMatrix);

            var rect = new SKRect(x - actualWidth / 2, -height, x + actualWidth / 2, 2 * height);

            DrawTexturedRectWithRotation(canvas, new(0, 0),
                rect, texture.Image, 360 - angle, new(rect.MidX, rect.MidY - judgeOffset / 2f), paint);

            target.PerfomenceMonitor.CountDrawCall(this);
            canvas.Restore();

            End();
        }

        public static void DrawTexturedRectWithRotation(
            SKCanvas canvas,
            SKPoint pos,
            SKRect rect,
            SKImage texture,
            float rotationDegrees,
            SKPoint pivot,
            SKPaint paint = null)
        {
            // 确定旋转中心点
            //SKPoint pivot = new SKPoint(rect.Left + origin.X * rect.Width, rect.Top + origin.Y * rect.Height);

            canvas.Save();

            // 应用变换矩阵
            SKMatrix matrix = SKMatrix.CreateTranslation(-pivot.X, -pivot.Y);
            matrix = matrix.PostConcat(SKMatrix.CreateRotationDegrees(rotationDegrees));
            matrix = matrix.PostConcat(SKMatrix.CreateTranslation(pivot.X, pivot.Y));
            matrix = matrix.PostConcat(SKMatrix.CreateTranslation(pos.X, pos.Y));

            canvas.SetMatrix(matrix);

            canvas.DrawImage(texture, rect, paint);

            canvas.Restore();
        }
    }
}
