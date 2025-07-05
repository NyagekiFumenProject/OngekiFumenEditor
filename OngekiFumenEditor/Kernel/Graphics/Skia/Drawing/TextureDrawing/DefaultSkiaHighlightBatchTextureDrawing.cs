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
using System.Windows.Media.Imaging;
using Vector2 = System.Numerics.Vector2;
using Vector3 = OpenTK.Mathematics.Vector3;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.TextureDrawing
{
    internal class DefaultSkiaHighlightBatchTextureDrawing : CommonSkiaDrawingBase, IHighlightBatchTextureDrawing
    {
        private SkiaImage texture;
        private List<(Vector2, Vector2, float, Vector4 color)> list = new();
        private SKCanvas canvas;
        private IDrawingContext target;

        public DefaultSkiaHighlightBatchTextureDrawing(DefaultSkiaDrawingManagerImpl manager) : base(manager)
        {

        }

        public void Begin(IDrawingContext target, IImage texture)
        {
            OnBegin(target);

            this.texture = texture as SkiaImage;
            list.Clear();
            canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;
            this.target = target;
        }

        public void Draw(IDrawingContext target, IImage texture, IEnumerable<(Vector2 size, Vector2 position, float rotation, Vector4 color)> instances)
        {
            Begin(target, texture);
            list.AddRange(instances);
            End();
        }

        public void End()
        {
            DoDraw();

            OnEnd();
            texture = default;
            target = default;
        }

        private void DoDraw()
        {
            using var paint = new SKPaint();
            using var maskfilter = SKMaskFilter.CreateBlur(SKBlurStyle.Inner, 5f);
            using var colorFilter = SKColorFilter.CreateColorMatrix([
                0.5f, 0.5f, 0.0f, 0.0f, 0.2f,  // 红色通道 = 0.5*R + 0.5*G + 0.2
                0.5f, 0.5f, 0.0f, 0.0f, 0.2f,  // 绿色通道 = 0.5*R + 0.5*G + 0.2
                0.0f, 0.0f, 0.0f, 0.0f, 0.0f,  // 蓝色通道 = 0
                0.0f, 0.0f, 0.0f, 0.75f, 0.0f   // Alpha通道保持不变
            ]);
            paint.MaskFilter = maskfilter;
            paint.ColorFilter = colorFilter;

            foreach (var (size, position, rotation, _) in list)
            {
                canvas.Save();
                var adjustPosition = position.ToSkiaSharpPoint();
                var adjustSize = new Vector2(Math.Abs(size.X), Math.Abs(size.Y));

                canvas.Translate(adjustPosition.X, adjustPosition.Y);
                canvas.RotateRadians(rotation);
                canvas.Scale(Math.Sign(size.X), -1 * Math.Sign(size.Y));
                var rect = SKRect.Create(-adjustSize.X / 2,
                    -adjustSize.Y / 2,
                    adjustSize.X,
                    adjustSize.Y);

                canvas.DrawImage(texture.Image, rect, paint);
                target.PerfomenceMonitor.CountDrawCall(this);
                canvas.Restore();
            }
        }

        public void PostSprite(Vector2 size, Vector2 position, float rotation, Vector4 color)
        {
            list.Add((size, position, rotation, color));
        }

        public void PostSprite(Vector2 size, Vector2 position, float rotation) => PostSprite(size, position, rotation, default);
    }
}
