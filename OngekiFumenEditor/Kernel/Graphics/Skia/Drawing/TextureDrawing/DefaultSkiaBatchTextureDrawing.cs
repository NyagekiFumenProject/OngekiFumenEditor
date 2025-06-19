using OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums;
using OngekiFumenEditor.Kernel.Graphics.OpenGL;
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Base;
using OngekiFumenEditor.Kernel.Graphics.Skia.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Utils;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Media.Imaging;
using Vector2 = System.Numerics.Vector2;
using Vector3 = OpenTK.Mathematics.Vector3;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.TextureDrawing
{
    internal class DefaultSkiaBatchTextureDrawing : CommonSkiaDrawingBase, IBatchTextureDrawing
    {
        private SkiaImage texture;
        private List<(Vector2, Vector2, float)> list = new();
        private SKCanvas canvas;
        private IDrawingContext target;

        public DefaultSkiaBatchTextureDrawing(DefaultSkiaDrawingManager manager) : base(manager)
        {

        }

        public void Begin(IDrawingContext target, IImage texture)
        {
            OnBegin(target);
            this.texture = texture as SkiaImage;
            canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;
            list.Clear();
            this.target = target;
        }

        public void Draw(IDrawingContext target, IImage texture, IEnumerable<(Vector2 size, Vector2 position, float rotation)> instances)
        {
            Begin(target, texture);
            list.AddRange(instances);
            End();
        }

        public void End()
        {
            DoDraw();

            OnEnd();

            target = default;
            canvas = default;
            texture = default;
        }

        private void DoDraw()
        {
            using var paint = new SKPaint();

            foreach (var (size, position, rotation) in list)
            {
                canvas.Save();
                var adjustPosition = position.ToSkiaSharpPoint();
                var adjustSize = new Vector2(Math.Abs(size.X), Math.Abs(size.Y));

                canvas.Translate(adjustPosition.X, adjustPosition.Y);
                canvas.RotateDegrees(rotation);
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

        public void PostSprite(Vector2 size, Vector2 position, float rotation)
        {
            list.Add((size, position, rotation));
        }
    }
}
