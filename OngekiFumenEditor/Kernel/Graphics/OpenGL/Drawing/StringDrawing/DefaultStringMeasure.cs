using FontStashSharp;
using OngekiFumenEditor.Kernel.Graphics.Text;
using OngekiFumenEditor.Properties;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.StringDrawing
{
    /// <summary>
    /// GL 侧字体表 / 样式解析 / 度量缓存，以及 Skia 字形图集的持有者。
    /// <para>
    /// 替换历史实现（FontStashSharp + FreeType，按 <c>字号 × FontResolutionFactor</c> 光栅化再双线性缩回）：
    /// 那条链路会让 hinting 在缩放时被摊平，且 1.5.1 的度量按 <c>字号 × factor²</c> 查询、与显示尺寸不一致，
    /// 结果是文字发虚且清晰度随定位相位波动。现在统一为「按最终像素尺寸带 hinting 光栅化」。
    /// </para>
    /// </summary>
    internal sealed class DefaultStringMeasure : IStringMeasure, IDisposable
    {
        private const int MaxMeasureTextCacheCount = 4096;

        private readonly Dictionary<(IFontHandle handle, FontStyle style), ResolvedTextStyle> cacheResolvedTextStyles = new();
        private readonly Dictionary<MeasureTextCacheKey, Vector2> cacheMeasureTextSizes = new();
        private readonly Queue<MeasureTextCacheKey> cacheMeasureTextSizeOrder = new();

        private readonly SkiaGlyphAtlas glyphAtlas = new();

        public static IEnumerable<IFontHandle> DefaultSupportFonts { get; } = GetSupportFonts();
        public static IFontHandle DefaultFont { get; } = DefaultSupportFonts.FirstOrDefault(x => x.FamilyName.ToLower() == "consola");

        public IEnumerable<IFontHandle> SupportFonts => DefaultSupportFonts;

        internal SkiaGlyphAtlas GlyphAtlas => glyphAtlas;

        public Vector2 MeasureString(string text, Vector2 scale, int fontSize, FontStyle style, IFontHandle handle)
        {
            text ??= string.Empty;
            handle ??= DefaultFont;

            var resolvedStyle = ResolveTextStyle(handle, style);

            var key = new MeasureTextCacheKey(text, resolvedStyle.FontHandle, fontSize, scale, resolvedStyle.FontStyle);
            if (cacheMeasureTextSizes.TryGetValue(key, out var size))
                return size;

            var filePath = GetFontFilePath(resolvedStyle.FontHandle);
            using var font = CreateFont(filePath, Math.Max(1, fontSize));
            font.MeasureText(text, out var bounds);

            size = new Vector2(bounds.Width * Math.Abs(scale.X), bounds.Height * Math.Abs(scale.Y));
            cacheMeasureTextSizes[key] = size;
            cacheMeasureTextSizeOrder.Enqueue(key);

            while (cacheMeasureTextSizes.Count > MaxMeasureTextCacheCount && cacheMeasureTextSizeOrder.TryDequeue(out var oldKey))
                cacheMeasureTextSizes.Remove(oldKey);

            return size;
        }

        /// <summary>按指定像素尺寸创建用于光栅化/度量的 Skia 字体。</summary>
        internal SKFont CreateFont(string filePath, int pixelSize)
        {
            return new SKFont(glyphAtlas.GetTypeface(filePath), pixelSize)
            {
                Hinting = SKFontHinting.Full,
                Edging = ProgramSetting.Default.DisableStringRendererAntialiasing ? SKFontEdging.Alias : SKFontEdging.Antialias,
                Subpixel = false,
            };
        }

        internal static string GetFontFilePath(IFontHandle handle)
        {
            return (handle as FontHandle)?.FilePath
                ?? throw new ArgumentException($"字体句柄缺少文件路径：{handle?.FamilyName}", nameof(handle));
        }

        internal void RebuildFontSystem()
        {
            cacheResolvedTextStyles.Clear();
            cacheMeasureTextSizes.Clear();
            cacheMeasureTextSizeOrder.Clear();
            glyphAtlas.Dispose();
        }

        internal ResolvedTextStyle ResolveTextStyle(IFontHandle handle, FontStyle style)
        {
            var key = (handle, style);
            if (cacheResolvedTextStyles.TryGetValue(key, out var resolvedStyle))
                return resolvedStyle;

            var fontStyle = TextStyle.None;

            if (style.HasFlag(FontStyle.Underline))
                fontStyle = TextStyle.Underline;
            if (style.HasFlag(FontStyle.Strike))
                fontStyle = TextStyle.Strikethrough;

            var resolvedHandle = handle;
            var isBold = style.HasFlag(FontStyle.Bold);
            var isItalic = style.HasFlag(FontStyle.Italic);

            if (isBold && isItalic)
                resolvedHandle = TryGetSubFont(handle, "z") ?? resolvedHandle;
            else if (isBold)
                resolvedHandle = TryGetSubFont(handle, "b") ?? resolvedHandle;
            else if (isItalic)
                resolvedHandle = TryGetSubFont(handle, "i") ?? resolvedHandle;

            resolvedStyle = new ResolvedTextStyle(resolvedHandle, fontStyle);
            cacheResolvedTextStyles[key] = resolvedStyle;
            return resolvedStyle;
        }

        private static IReadOnlyList<IFontHandle> GetSupportFonts()
        {
            return Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.Fonts)).Select(x => new FontHandle
            {
                FamilyName = Path.GetFileNameWithoutExtension(x),
                FilePath = x
            }).Where(x => Path.GetExtension(x.FilePath).Equals(".ttf", StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        private static IFontHandle TryGetSubFont(IFontHandle handle, string sub)
        {
            if (handle?.FamilyName == null)
                return default;

            return SupportFontMap.TryGetValue(handle.FamilyName + sub, out var subFont) ? subFont : default;
        }

        private static readonly IReadOnlyDictionary<string, IFontHandle> SupportFontMap =
            DefaultSupportFonts.ToDictionary(x => x.FamilyName, StringComparer.OrdinalIgnoreCase);

        internal readonly struct ResolvedTextStyle
        {
            public IFontHandle FontHandle { get; }
            public TextStyle FontStyle { get; }

            public ResolvedTextStyle(IFontHandle fontHandle, TextStyle fontStyle)
            {
                FontHandle = fontHandle;
                FontStyle = fontStyle;
            }
        }

        private sealed class FontHandle : IFontHandle
        {
            public string FamilyName { get; set; }
            public string FilePath { get; set; }
        }

        private readonly struct MeasureTextCacheKey : IEquatable<MeasureTextCacheKey>
        {
            private readonly string text;
            private readonly IFontHandle handle;
            private readonly int fontSize;
            private readonly Vector2 scale;
            private readonly TextStyle style;

            public MeasureTextCacheKey(string text, IFontHandle handle, int fontSize, Vector2 scale, TextStyle style)
            {
                this.text = text;
                this.handle = handle;
                this.fontSize = fontSize;
                this.scale = scale;
                this.style = style;
            }

            public bool Equals(MeasureTextCacheKey other) =>
                text == other.text && Equals(handle, other.handle) && fontSize == other.fontSize &&
                scale == other.scale && style == other.style;

            public override bool Equals(object obj) => obj is MeasureTextCacheKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(text, handle, fontSize, scale, style);
        }

        public void Dispose()
        {
            glyphAtlas.Dispose();
            cacheResolvedTextStyles.Clear();
            cacheMeasureTextSizes.Clear();
            cacheMeasureTextSizeOrder.Clear();
        }
    }
}
