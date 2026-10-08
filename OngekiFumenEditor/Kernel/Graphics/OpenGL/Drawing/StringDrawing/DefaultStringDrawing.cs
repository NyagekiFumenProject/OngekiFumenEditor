using FontStashSharp;
using OngekiFumenEditor.Kernel.Graphics.Text;
using OngekiFumenEditor.Properties;
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
    /// <para>
    /// 连续的文字命令会共用同一个批次（同一绘制目标 + 同一 MVP），由
    /// <see cref="OpenGLDrawCommandListReplay"/> 在一串文字的最后一条命令处收尾：字形本身很便宜，
    /// 真正贵的是每次 <c>DrawArrays</c>，批次边界直接决定文字渲染成本。
    /// </para>
    /// </summary>
    internal sealed class DefaultStringDrawing : CommonOpenGLDrawingBase, IStringDrawing, IDisposable
    {
        private readonly DefaultStringMeasure stringMeasure = new();
        private readonly SkiaGlyphRenderer renderer = new();

        private bool batching;
        private Matrix4x4 batchMvp;
        private IDrawingContext batchTarget;

        public static IEnumerable<IFontHandle> DefaultSupportFonts => DefaultStringMeasure.DefaultSupportFonts;
        public static IFontHandle DefaultFont => DefaultStringMeasure.DefaultFont;

        public IEnumerable<IFontHandle> SupportFonts => stringMeasure.SupportFonts;

        public DefaultStringDrawing(DefaultOpenGLRenderManagerImpl manager) : base(manager)
        {
            // 图集写满会整体重建，已收集但还没提交的字形必须在此之前提交。
            stringMeasure.GlyphAtlas.BeforeReset = Flush;
        }

        public void Draw(string text, Vector2 pos, Vector2 scale, int fontSize, float rotate, Vector4 color, Vector2 origin, FontStyle style, IDrawingContext target, IFontHandle handle, out Vector2? measureTextSize)
        {
            text ??= string.Empty;
            if (scale.X == 0 || scale.Y == 0)
            {
                measureTextSize = Vector2.Zero;
                return;
            }
            handle ??= DefaultStringMeasure.DefaultFont;

            var resolvedStyle = stringMeasure.ResolveTextStyle(handle, style);
            var filePath = DefaultStringMeasure.GetFontFilePath(resolvedStyle.FontHandle);
            var disableAntialiasing = ProgramSetting.Default.DisableStringRendererAntialiasing;

            // 关键：按「最终像素尺寸」光栅化（字号 × 纵向缩放），hinting 才真正生效。
            var pixelSize = Math.Max(1, (int)Math.Round(fontSize * Math.Abs(scale.Y)));
            var fontId = stringMeasure.GlyphAtlas.GetFontId(filePath);

            var textBox = stringMeasure.GetTextBounds(text, fontId, pixelSize, disableAntialiasing);
            var boxWidth = textBox.Width;
            var boxHeight = textBox.Height;

            // 拉伸比：字形按纵向缩放光栅化，横向差值转成四边形宽度比（缩放一致时恒为 1）。
            var stretchX = scale.X / Math.Abs(scale.Y);
            var scaleY = Math.Sign(scale.Y);
            measureTextSize = new Vector2(boxWidth * Math.Abs(stretchX), boxHeight);

            if (text.Length == 0 || boxWidth <= 0 || boxHeight <= 0)
                return;

            var atlas = stringMeasure.GlyphAtlas;

            // 顶点在 GPU 上按批次 MVP 变换，所以只有「同一绘制目标 + 同一 MVP」才能并进同一批，否则先把上一批提交掉。
            var mvp = GetOverrideModelMatrix() * GetOverrideViewProjectMatrixOrDefault(target.CurrentDrawingTargetContext);
            if (batching && (!ReferenceEquals(batchTarget, target) || mvp != batchMvp))
                Flush();
            OpenBatch(mvp, target);

            // 与 Skia 后端相同的原点换算：origin 以文本盒为单位，0.5 表示居中。
            var offsetX = textBox.Left + origin.X * boxWidth;
            var offsetY = -textBox.Top - origin.Y * boxHeight;

            var cos = rotate == 0 ? 1f : MathF.Cos(rotate);
            var sin = rotate == 0 ? 0f : MathF.Sin(rotate);

            var penX = -offsetX * stretchX;
            var baselineY = -offsetY;

            // 1:1 绘制时把字形吸附到整数像素：hinting 只有落在像素网格上才真正锐利，
            // 带小数的笔位会被双线性采样重新抹平（这正是旧实现清晰度随定位相位波动的原因）。
            var snapToPixels = rotate == 0 && Math.Abs(stretchX - 1) < 1e-3f && Math.Abs(Math.Abs(scale.Y) - 1) < 1e-3f;

            void PushLocal(Vector2 topLeft, float width, float height, Vector4 uv)
            {
                var localX = topLeft.X + penX;
                var localY = (topLeft.Y + baselineY) * scaleY;
                if (snapToPixels)
                {
                    localX = MathF.Round(localX);
                    localY = MathF.Round(localY);
                }

                // 局部坐标（相对 pos，y 向上）→ 旋转 → 平移到 pos。
                Vector2 Rotate(Vector2 local) => new(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos);

                var tl = pos + Rotate(new Vector2(localX, localY));
                var tr = pos + Rotate(new Vector2(localX + width, localY));
                var br = pos + Rotate(new Vector2(localX + width, localY - height * scaleY));
                var bl = pos + Rotate(new Vector2(localX, localY - height * scaleY));

                renderer.PushQuad(tl, tr, br, bl, uv, color);
            }

            foreach (var run in stringMeasure.GetFontRuns(text, fontId))
            {
                var runFontId = atlas.GetFontId(run.Typeface);
                foreach (var rune in run.Text.EnumerateRunes())
                {
                    var glyph = atlas.GetGlyph(runFontId, rune.Value, pixelSize, disableAntialiasing);

                    // 取字形时可能刚好触发图集重建，这里重新开批继续收集。
                    OpenBatch(mvp, target);

                    PushLocal(new Vector2(glyph.OffsetX * stretchX, glyph.Top), glyph.Width * stretchX, glyph.Height,
                        new Vector4(glyph.U0, glyph.V0, glyph.U1, glyph.V1));

                    penX += glyph.Advance * stretchX;

                    if (renderer.IsFull)
                    {
                        Flush();
                        OpenBatch(mvp, target);
                    }
                }
            }

            // 下划线/删除线：用图集里的 1x1 实心条目画成细矩形。
            if (resolvedStyle.FontStyle != TextStyle.None)
            {
                var font = stringMeasure.GetFont(fontId, pixelSize, disableAntialiasing);
                font.GetFontMetrics(out var metrics);
                var solid = atlas.GetSolid(fontId);
                var solidUv = new Vector4(solid.U0, solid.V0, solid.U1, solid.V1);
                var thickness = Math.Max(1f, metrics.UnderlineThickness ?? 1f);

                // 画线位置以基线为参照（世界坐标 y 向上）：下划线在基线下方，删除线在 x 高度中线。
                var lineY = resolvedStyle.FontStyle == TextStyle.Underline
                    ? -(metrics.UnderlinePosition ?? thickness)
                    : metrics.XHeight / 2;

                PushLocal(new Vector2(textBox.Left * stretchX, lineY), boxWidth * stretchX, thickness, solidUv);
            }
        }

        /// <summary>
        /// 提交当前批次收集到的全部字形。由命令重放在一串连续文字的最后一条 <c>DrawStringCommand</c> 内侧调用，
        /// 这样既能把上百条标签并成一次 <c>DrawArrays</c>，又不会越过它后面任何非文字绘制命令（绘制顺序保持不变）。
        /// </summary>
        public void Flush()
        {
            if (!batching)
                return;

            batching = false;
            var target = batchTarget;
            batchTarget = null;
            renderer.End(stringMeasure.GlyphAtlas.Texture, target);
        }

        /// <summary>
        /// 丢弃尚未提交的字形。命令重放开始时调用：批次只在一个命令列表内有效，异常中断也不会把顶点带到下一帧。
        /// </summary>
        public void DiscardPendingBatch()
        {
            batching = false;
            batchTarget = null;
            renderer.Discard();
        }

        private void OpenBatch(Matrix4x4 mvp, IDrawingContext target)
        {
            if (batching)
                return;

            batching = true;
            batchMvp = mvp;
            batchTarget = target;
            renderer.Begin(mvp);
        }

        public void Dispose()
        {
            batching = false;
            batchTarget = null;
            renderer?.Dispose();
            stringMeasure?.Dispose();
        }
    }
}
