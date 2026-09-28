using FontStashSharp;
using OngekiFumenEditor.Kernel.Graphics.Text;
using OngekiFumenEditor.Properties;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.StringDrawing
{
    /// <summary>
    /// GL 文字绘制：用 SkiaSharp 在「最终像素尺寸」上带 hinting 光栅化的字形，拼成一个批次一次画完。
    /// <para>
    /// 排版数学与 Skia 后端保持一致（同一套 <c>MeasureText</c> + origin/scale/rotate 语义），
    /// 因此原先「GL 与 Skia 文字位置不同」的问题也一并消除。
    /// </para>
    /// </summary>
    internal sealed class DefaultStringDrawing : CommonOpenGLDrawingBase, IStringDrawing, IDisposable
    {
        private readonly DefaultStringMeasure stringMeasure = new();
        private readonly SkiaGlyphRenderer renderer = new();

        public static IEnumerable<IFontHandle> DefaultSupportFonts => DefaultStringMeasure.DefaultSupportFonts;
        public static IFontHandle DefaultFont => DefaultStringMeasure.DefaultFont;

        public IEnumerable<IFontHandle> SupportFonts => stringMeasure.SupportFonts;

        public DefaultStringDrawing(DefaultOpenGLRenderManagerImpl manager) : base(manager)
        {
        }

        public void Draw(string text, Vector2 pos, Vector2 scale, int fontSize, float rotate, Vector4 color, Vector2 origin, FontStyle style, IDrawingContext target, IFontHandle handle, out Vector2? measureTextSize)
        {
            text ??= string.Empty;
            handle ??= DefaultStringMeasure.DefaultFont;

            var resolvedStyle = stringMeasure.ResolveTextStyle(handle, style);
            var filePath = DefaultStringMeasure.GetFontFilePath(resolvedStyle.FontHandle);

            // 关键：按「最终像素尺寸」光栅化（字号 × 纵向缩放），hinting 才真正生效。
            var pixelSize = Math.Max(1, (int)Math.Round(fontSize * Math.Abs(scale.Y)));
            using var font = stringMeasure.CreateFont(filePath, pixelSize);
            using var paint = new SKPaint { IsAntialias = !ProgramSetting.Default.DisableStringRendererAntialiasing };

            font.MeasureText(text, out var bounds, paint);
            var boxWidth = bounds.Width;
            var boxHeight = bounds.Height;

            measureTextSize = new Vector2(boxWidth * Math.Abs(scale.X), boxHeight * Math.Abs(scale.Y));

            if (text.Length == 0 || boxWidth <= 0 || boxHeight <= 0)
                return;

            var atlas = stringMeasure.GlyphAtlas;
            var disableAntialiasing = ProgramSetting.Default.DisableStringRendererAntialiasing;

            // 与 Skia 后端相同的原点换算：origin 以文本盒为单位，0.5 表示居中。
            var offsetX = origin.X * boxWidth;
            var offsetY = boxHeight - origin.Y * boxHeight;

            // 拉伸比：字形按纵向缩放光栅化，横向差值转成四边形宽度比（缩放一致时恒为 1）。
            var stretchX = Math.Abs(scale.Y) < float.Epsilon ? 1f : scale.X / Math.Abs(scale.Y);
            var scaleY = Math.Sign(scale.Y) == 0 ? 1f : Math.Sign(scale.Y);

            var cos = rotate == 0 ? 1f : MathF.Cos(rotate);
            var sin = rotate == 0 ? 0f : MathF.Sin(rotate);

            var penX = -offsetX * stretchX;
            var baselineY = -offsetY;

            var mvp = GetOverrideModelMatrix() * GetOverrideViewProjectMatrixOrDefault(target.CurrentDrawingTargetContext);
            renderer.Begin(mvp);

            // 1:1 绘制时把字形吸附到整数像素：hinting 只有落在像素网格上才真正锐利，
            // 带小数的笔位会被双线性采样重新抹平（这正是旧实现清晰度随定位相位波动的原因）。
            var snapToPixels = rotate == 0 && Math.Abs(stretchX - 1) < 1e-3f && Math.Abs(Math.Abs(scale.Y) - 1) < 1e-3f;

            void PushLocal(Vector2 topLeft, float width, float height, Vector4 uv)
            {
                var localX = topLeft.X + penX;
                var localY = topLeft.Y + baselineY;
                if (snapToPixels)
                {
                    localX = MathF.Round(localX);
                    localY = MathF.Round(localY);
                }

                // 局部坐标（相对 pos，y 向上）→ 旋转 → 平移到 pos。
                Vector2 Rotate(Vector2 local) => new(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos);

                var tl = pos + Rotate(new Vector2(localX, localY));
                var tr = pos + Rotate(new Vector2(localX + width, localY));
                var br = pos + Rotate(new Vector2(localX + width, localY - height));
                var bl = pos + Rotate(new Vector2(localX, localY - height));

                renderer.PushQuad(tl, tr, br, bl, uv, color);
            }

            for (var i = 0; i < text.Length; i++)
            {
                var codepoint = char.ConvertToUtf32(text, i);
                if (char.IsHighSurrogate(text[i]))
                    i++;

                var glyph = atlas.GetGlyph(filePath, codepoint, pixelSize, disableAntialiasing);

                PushLocal(new Vector2(glyph.OffsetX * stretchX, glyph.Top), glyph.Width * stretchX, glyph.Height,
                    new Vector4(glyph.U0, glyph.V0, glyph.U1, glyph.V1));

                penX += glyph.Advance * stretchX;
            }

            // 下划线/删除线：用图集里的 1x1 实心条目画成细矩形。
            if (resolvedStyle.FontStyle != TextStyle.None)
            {
                font.GetFontMetrics(out var metrics);
                var solid = atlas.GetSolid(filePath);
                var solidUv = new Vector4(solid.U0, solid.V0, solid.U1, solid.V1);
                var thickness = Math.Max(1f, metrics.UnderlineThickness ?? 1f);

                // 画线位置以基线为参照（世界坐标 y 向上）：下划线在基线下方，删除线在 x 高度中线。
                var lineY = resolvedStyle.FontStyle == TextStyle.Underline
                    ? -(metrics.UnderlinePosition ?? thickness)
                    : metrics.XHeight / 2;

                PushLocal(new Vector2(0, lineY), boxWidth * stretchX, thickness, solidUv);
            }

            renderer.End(atlas.Texture, target);
        }

        public void Dispose()
        {
            renderer?.Dispose();
            stringMeasure?.Dispose();
        }
    }
}
