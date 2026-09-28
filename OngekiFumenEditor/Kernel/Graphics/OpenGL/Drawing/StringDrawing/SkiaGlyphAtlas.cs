using OngekiFumenEditor.Kernel.Graphics.OpenGL.Base;
using OpenTK.Graphics.OpenGL4;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.StringDrawing
{
    /// <summary>
    /// 用 SkiaSharp 在「最终像素尺寸」上带 hinting 光栅化字形，并打包进一张 GL 图集纹理。
    /// <para>
    /// 与旧的 FontStashSharp 路线（按 <c>字号 × FontResolutionFactor</c> 光栅化再双线性缩回）相比：
    /// hinting 在最终尺寸上生效、不会被缩放摊平，且度量与显示尺寸一致——这正是「GL 文字发虚」的根因。
    /// </para>
    /// <para>
    /// 图集内容为「白色 RGB + 覆盖率写在 A 通道」，因此可以直接与顶点色相乘
    /// （着色器约定为 <c>texture(diffuse,uv) * color</c>），无需处理 premultiplied alpha。
    /// 位图按行上传，v=0 对应位图首行（即字形顶部），因此绘制时上方顶点取 V0、下方取 V1，不做翻转。
    /// </para>
    /// </summary>
    internal sealed class SkiaGlyphAtlas : IDisposable
    {
        /// <summary>图集边长（1024² RGBA = 4MB）。装满后整体重建。</summary>
        private const int AtlasSize = 1024;

        /// <summary>字形四周留白，避免 Linear 采样时相邻字形串色。</summary>
        private const float GlyphPadding = 2;

        /// <summary>1x1 白色实心条目，用于下划线/删除线。</summary>
        private const int SolidCodepoint = -1;

        internal readonly struct AtlasKey : IEquatable<AtlasKey>
        {
            public readonly string FontFilePath;
            public readonly int Codepoint;
            public readonly int PixelSize;
            public readonly bool DisableAntialiasing;

            public AtlasKey(string fontFilePath, int codepoint, int pixelSize, bool disableAntialiasing)
            {
                FontFilePath = fontFilePath;
                Codepoint = codepoint;
                PixelSize = pixelSize;
                DisableAntialiasing = disableAntialiasing;
            }

            public bool Equals(AtlasKey other) =>
                Codepoint == other.Codepoint && PixelSize == other.PixelSize &&
                DisableAntialiasing == other.DisableAntialiasing &&
                string.Equals(FontFilePath, other.FontFilePath, StringComparison.OrdinalIgnoreCase);

            public override bool Equals(object obj) => obj is AtlasKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(
                FontFilePath?.ToLowerInvariant(), Codepoint, PixelSize, DisableAntialiasing);
        }

        /// <summary>一个字形在图集中的位置与放置信息（像素单位）。</summary>
        internal readonly struct AtlasEntry
        {
            public readonly float U0, V0, U1, V1;

            /// <summary>字形位图尺寸（像素），含四周留白。</summary>
            public readonly int Width, Height;

            /// <summary>笔位（基线起点）到位图左边缘的偏移（y 向上坐标系）。</summary>
            public readonly float OffsetX;

            /// <summary>基线（笔位）到位图上边缘的偏移（y 向上坐标系，上边缘在基线上方为正）。</summary>
            public readonly float Top;

            /// <summary>推进宽度（像素，非取整）。</summary>
            public readonly float Advance;

            public AtlasEntry(float u0, float v0, float u1, float v1, int width, int height,
                float offsetX, float top, float advance)
            {
                U0 = u0;
                V0 = v0;
                U1 = u1;
                V1 = v1;
                Width = width;
                Height = height;
                OffsetX = offsetX;
                Top = top;
                Advance = advance;
            }
        }

        private readonly Dictionary<AtlasKey, AtlasEntry> entries = new();
        private readonly Dictionary<string, SKTypeface> typefaces = new(StringComparer.OrdinalIgnoreCase);
        private readonly object syncRoot = new();

        private DefaultOpenGLTexture texture;
        private byte[] pixelBuffer;
        private int shelfX, shelfY, shelfHeight;

        public DefaultOpenGLTexture Texture => texture;
        public bool IsEmpty => texture == null;

        /// <summary>取字形条目；不存在则光栅化并写进图集。必须在有 GL 上下文的线程上调用。</summary>
        public AtlasEntry GetGlyph(string fontFilePath, int codepoint, int pixelSize, bool disableAntialiasing)
        {
            var key = new AtlasKey(fontFilePath, codepoint, pixelSize, disableAntialiasing);
            if (entries.TryGetValue(key, out var cached))
                return cached;

            EnsureAtlas();

            var typeface = GetTypeface(fontFilePath);
            var rendered = Rasterize(typeface, codepoint, pixelSize, disableAntialiasing);

            var allocated = Allocate(rendered.Width, rendered.Height);
            if (allocated is null)
            {
                // 图集满了：整体重建（旧条目全部失效）后再试一次。
                ResetAtlas();
                allocated = Allocate(rendered.Width, rendered.Height)
                    ?? throw new InvalidOperationException($"字形 {rendered.Width}x{rendered.Height} 放不进 {AtlasSize}² 图集。");
            }

            var entry = allocated.Value;
            // Allocate 只决定图集位置，字形自身的放置信息（偏移/推进）来自光栅化结果。
            entry = new AtlasEntry(entry.U0, entry.V0, entry.U1, entry.V1,
                rendered.Width, rendered.Height, rendered.OffsetX, rendered.Top, rendered.Advance);
            Upload(entry, rendered);
            entries[key] = entry;

            return entry;
        }

        /// <summary>1x1 白色实心条目（下划线/删除线用）。</summary>
        public AtlasEntry GetSolid(string fontFilePath) => GetGlyph(fontFilePath, SolidCodepoint, 1, true);

        internal SKTypeface GetTypeface(string fontFilePath)
        {
            lock (syncRoot)
            {
                if (typefaces.TryGetValue(fontFilePath, out var cached))
                    return cached;

                var typeface = SKTypeface.FromFile(fontFilePath)
                    ?? throw new InvalidOperationException($"无法加载字体文件：{fontFilePath}");

                typefaces[fontFilePath] = typeface;
                return typeface;
            }
        }

        private readonly struct Rendered
        {
            public readonly byte[] Pixels;
            public readonly int Width, Height;
            public readonly float OffsetX, Top, Advance;

            public Rendered(byte[] pixels, int width, int height, float offsetX, float top, float advance)
            {
                Pixels = pixels;
                Width = width;
                Height = height;
                OffsetX = offsetX;
                Top = top;
                Advance = advance;
            }
        }

        /// <summary>把单个字形画成「白色 RGB + A=覆盖率」的直通位图（可整块上传）。</summary>
        private static Rendered Rasterize(SKTypeface typeface, int codepoint, int pixelSize, bool disableAntialiasing)
        {
            if (codepoint == SolidCodepoint)
                return new Rendered(new byte[] { 255, 255, 255, 255 }, 1, 1, 0, 1, 1);

            using var font = new SKFont(typeface, pixelSize)
            {
                Hinting = SKFontHinting.Full,
                Edging = disableAntialiasing ? SKFontEdging.Alias : SKFontEdging.Antialias,
                Subpixel = false,
            };
            font.GetFontMetrics(out var metrics);

            var text = char.ConvertFromUtf32(codepoint);
            var advance = font.MeasureText(text);

            var width = Math.Max(1, (int)Math.Ceiling(advance) + (int)GlyphPadding * 2);
            var baselineY = -metrics.Ascent + GlyphPadding;
            var height = Math.Max(1, (int)Math.Ceiling(baselineY + metrics.Descent + GlyphPadding));

            using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                using var paint = new SKPaint { Color = SKColors.White, IsAntialias = !disableAntialiasing };
                canvas.DrawText(text, GlyphPadding, baselineY, font, paint);
                canvas.Flush();
            }

            var pixels = bitmap.GetPixelSpan().ToArray();

            // 位图左上角相对「基线起点」的偏移：水平 -留白；竖直：上边缘在基线上方 baselineY。
            return new Rendered(pixels, width, height, -GlyphPadding, baselineY, advance);
        }

        private void EnsureAtlas()
        {
            if (texture != null)
                return;

            var handle = GL.GenTexture();
            OpenGLTextureBindingCache.BindTexture2D(TextureUnit.Texture0, handle);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, AtlasSize, AtlasSize, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            // 关闭 mipmap 生成：字形是按最终尺寸光栅化的，缩放交给采样即可（旧实现正是在这里丢了清晰度）。
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);

            texture = new DefaultOpenGLTexture(handle, AtlasSize, AtlasSize, null, "SkiaGlyphAtlas");
            pixelBuffer = new byte[AtlasSize * AtlasSize * 4];

            shelfX = shelfY = shelfHeight = 0;
        }

        private void ResetAtlas()
        {
            entries.Clear();
            shelfX = shelfY = shelfHeight = 0;
            Array.Clear(pixelBuffer, 0, pixelBuffer.Length);

            OpenGLTextureBindingCache.BindTexture2D(TextureUnit.Texture0, texture.ID);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, AtlasSize, AtlasSize,
                PixelFormat.Rgba, PixelType.UnsignedByte, pixelBuffer);
        }

        private AtlasEntry? Allocate(int width, int height)
        {
            if (width > AtlasSize || height > AtlasSize)
                return null;

            if (shelfX + width > AtlasSize)
            {
                shelfY += shelfHeight;
                shelfX = 0;
                shelfHeight = 0;
            }

            if (shelfY + height > AtlasSize)
                return null;

            var entry = new AtlasEntry(
                shelfX / (float)AtlasSize,
                shelfY / (float)AtlasSize,
                (shelfX + width) / (float)AtlasSize,
                (shelfY + height) / (float)AtlasSize,
                width, height,
                0, 0, 0);

            shelfX += width;
            shelfHeight = Math.Max(shelfHeight, height);

            return entry;
        }

        private void Upload(AtlasEntry entry, Rendered rendered)
        {
            var x = (int)Math.Round(entry.U0 * AtlasSize);
            var y = (int)Math.Round(entry.V0 * AtlasSize);

            for (var row = 0; row < rendered.Height; row++)
                System.Buffer.BlockCopy(rendered.Pixels, row * rendered.Width * 4,
                    pixelBuffer, ((y + row) * AtlasSize + x) * 4, rendered.Width * 4);

            OpenGLTextureBindingCache.BindTexture2D(TextureUnit.Texture0, texture.ID);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, x, y, rendered.Width, rendered.Height,
                PixelFormat.Rgba, PixelType.UnsignedByte, rendered.Pixels);
        }

        public void Dispose()
        {
            texture?.Dispose();
            texture = null;
            pixelBuffer = null;

            foreach (var typeface in typefaces.Values)
                typeface.Dispose();
            typefaces.Clear();
            entries.Clear();
        }
    }
}
