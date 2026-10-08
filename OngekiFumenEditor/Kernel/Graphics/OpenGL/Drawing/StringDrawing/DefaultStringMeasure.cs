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
        private readonly SkiaFontFallback fontFallback = new();
        private readonly Dictionary<TextSizeCacheKey, SKRect> cacheTextSizes = new();
        private readonly Queue<TextSizeCacheKey> cacheTextSizeOrder = new();

        /// <summary>测量用画笔（沿用旧行为：只带抗锯齿开关，与字形设置一致）。</summary>
        private readonly SKPaint measurePaint = new() { IsAntialias = true };
        private readonly SKPaint measurePaintAliased = new() { IsAntialias = false };

        private readonly SkiaGlyphAtlas glyphAtlas = new();

        public static IEnumerable<IFontHandle> DefaultSupportFonts { get; } = GetSupportFonts();

        private static readonly object defaultFontSync = new();
        private static IFontHandle cachedDefaultFont;

        static DefaultStringMeasure()
        {
            ProgramSetting.Default.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ProgramSetting.EditorFontFamilyName))
                    InvalidateDefaultFontCache();
            };
        }

        /// <summary>
        /// 默认字体：随 <see cref="ProgramSetting.EditorFontFamilyName"/> 变化即时重解析（编辑器逐帧读取，无需重启）。
        /// </summary>
        public static IFontHandle DefaultFont
        {
            get
            {
                lock (defaultFontSync)
                    return cachedDefaultFont ??= ResolveDefaultFont();
            }
        }

        private static void InvalidateDefaultFontCache()
        {
            lock (defaultFontSync)
                cachedDefaultFont = null;
        }

        /// <summary>
        /// 解析默认字体：优先使用 <see cref="ProgramSetting.EditorFontFamilyName"/> 指定的家族名（按文件名去扩展名，大小写不敏感），
        /// 匹配不到则回退既有 <c>consola</c> 匹配，再取不到则退到第一个可用字体（不抛异常）。
        /// </summary>
        private static IFontHandle ResolveDefaultFont()
        {
            var configured = ProgramSetting.Default.EditorFontFamilyName;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                var matched = DefaultSupportFonts.FirstOrDefault(x => string.Equals(x.FamilyName, configured, StringComparison.OrdinalIgnoreCase));
                if (matched != null)
                    return matched;
            }

            return DefaultSupportFonts.FirstOrDefault(x => x.FamilyName.ToLower() == "consola")
                ?? DefaultSupportFonts.FirstOrDefault();
        }

        public IEnumerable<IFontHandle> SupportFonts => DefaultSupportFonts;

        internal SkiaGlyphAtlas GlyphAtlas => glyphAtlas;

        public Vector2 MeasureString(string text, Vector2 scale, int fontSize, FontStyle style, IFontHandle handle)
        {
            text ??= string.Empty;
            if (scale.X == 0 || scale.Y == 0)
                return Vector2.Zero;
            handle ??= DefaultFont;

            var resolvedStyle = ResolveTextStyle(handle, style);
            var disableAntialiasing = ProgramSetting.Default.DisableStringRendererAntialiasing;

            var key = new MeasureTextCacheKey(text, resolvedStyle.FontHandle, fontSize, scale, resolvedStyle.FontStyle, disableAntialiasing);
            if (cacheMeasureTextSizes.TryGetValue(key, out var size))
                return size;

            var filePath = GetFontFilePath(resolvedStyle.FontHandle);
            var pixelSize = Math.Max(1, (int)Math.Round(fontSize * Math.Abs(scale.Y)));
            var bounds = GetTextBounds(text, glyphAtlas.GetFontId(filePath), pixelSize, disableAntialiasing);
            var stretchX = scale.X / Math.Abs(scale.Y);

            // 纵向缩放已计入光栅化字号，测量必须与绘制使用同一个像素尺寸。
            size = new Vector2(bounds.Width * Math.Abs(stretchX), bounds.Height);
            cacheMeasureTextSizes[key] = size;
            cacheMeasureTextSizeOrder.Enqueue(key);

            while (cacheMeasureTextSizes.Count > MaxMeasureTextCacheCount && cacheMeasureTextSizeOrder.TryDequeue(out var oldKey))
                cacheMeasureTextSizes.Remove(oldKey);

            return size;
        }

        /// <summary>
        /// 取「按最终像素尺寸创建、带 hinting」的字体实例。每次绘制都新建原生 <see cref="SKFont"/>（并随即 Dispose）
        /// 是文字热路径上最大的一块固定开销，因此按 (字体, 像素尺寸, 抗锯齿) 复用。
        /// </summary>
        internal SKFont GetFont(int fontId, int pixelSize, bool disableAntialiasing) =>
            fontFallback.GetFont(glyphAtlas.GetTypeface(fontId), pixelSize, disableAntialiasing);

        internal SkiaFontFallback.FontRun[] GetFontRuns(string text, int fontId) =>
            fontFallback.GetRuns(glyphAtlas.GetTypeface(fontId), text);

        /// <summary>测量用画笔；返回的实例只读，调用方不得修改。</summary>
        internal SKPaint GetMeasurePaint(bool disableAntialiasing) => disableAntialiasing ? measurePaintAliased : measurePaint;

        /// <summary>
        /// 文本盒尺寸及其相对基线的位置（像素）。绘制路径每帧都会对同一串文字重新求布，而结果只与 (文字, 字体, 像素尺寸, 抗锯齿) 有关。
        /// </summary>
        internal SKRect GetTextBounds(string text, int fontId, int pixelSize, bool disableAntialiasing)
        {
            text ??= string.Empty;

            var key = new TextSizeCacheKey(text, fontId, pixelSize, disableAntialiasing);
            if (cacheTextSizes.TryGetValue(key, out var cached))
                return cached;

            fontFallback.MeasureText(glyphAtlas.GetTypeface(fontId), text, pixelSize, disableAntialiasing,
                GetMeasurePaint(disableAntialiasing), out var bounds);
            cacheTextSizes[key] = bounds;
            cacheTextSizeOrder.Enqueue(key);

            while (cacheTextSizes.Count > MaxMeasureTextCacheCount && cacheTextSizeOrder.TryDequeue(out var oldKey))
                cacheTextSizes.Remove(oldKey);

            return bounds;
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
            cacheTextSizes.Clear();
            cacheTextSizeOrder.Clear();
            fontFallback.Clear();
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
            try
            {
                var fontDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                if (string.IsNullOrEmpty(fontDir) || !Directory.Exists(fontDir))
                    return Array.Empty<IFontHandle>();

                return Directory.GetFiles(fontDir).Select(x => new FontHandle
                {
                    FamilyName = Path.GetFileNameWithoutExtension(x),
                    FilePath = x
                }).Where(x => Path.GetExtension(x.FilePath).Equals(".ttf", StringComparison.OrdinalIgnoreCase)).ToArray();
            }
            catch (Exception ex)
            {
                OngekiFumenEditor.Utils.Log.LogWarn($"Failed to enumerate system fonts: {ex.Message}");
                return Array.Empty<IFontHandle>();
            }
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
            private readonly bool disableAntialiasing;

            public MeasureTextCacheKey(string text, IFontHandle handle, int fontSize, Vector2 scale, TextStyle style, bool disableAntialiasing)
            {
                this.text = text;
                this.handle = handle;
                this.fontSize = fontSize;
                this.scale = scale;
                this.style = style;
                this.disableAntialiasing = disableAntialiasing;
            }

            public bool Equals(MeasureTextCacheKey other) =>
                text == other.text && Equals(handle, other.handle) && fontSize == other.fontSize &&
                scale == other.scale && style == other.style && disableAntialiasing == other.disableAntialiasing;

            public override bool Equals(object obj) => obj is MeasureTextCacheKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(text, handle, fontSize, scale, style, disableAntialiasing);
        }


        private readonly struct TextSizeCacheKey : IEquatable<TextSizeCacheKey>
        {
            private readonly string text;
            private readonly int fontId;
            private readonly int pixelSize;
            private readonly bool disableAntialiasing;

            public TextSizeCacheKey(string text, int fontId, int pixelSize, bool disableAntialiasing)
            {
                this.text = text;
                this.fontId = fontId;
                this.pixelSize = pixelSize;
                this.disableAntialiasing = disableAntialiasing;
            }

            public bool Equals(TextSizeCacheKey other) =>
                text == other.text && fontId == other.fontId && pixelSize == other.pixelSize &&
                disableAntialiasing == other.disableAntialiasing;

            public override bool Equals(object obj) => obj is TextSizeCacheKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(text, fontId, pixelSize, disableAntialiasing);
        }

        public void Dispose()
        {
            glyphAtlas.Dispose();
            cacheResolvedTextStyles.Clear();
            cacheMeasureTextSizes.Clear();
            cacheMeasureTextSizeOrder.Clear();
            cacheTextSizes.Clear();
            cacheTextSizeOrder.Clear();
            fontFallback.Dispose();
            measurePaint.Dispose();
            measurePaintAliased.Dispose();
        }
    }
}
