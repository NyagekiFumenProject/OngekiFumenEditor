using FontStashSharp;
using FontStashSharp.Rasterizers.FreeType;
using OngekiFumenEditor.Kernel.Graphics.Text;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.StringDrawing
{
    internal sealed class DefaultStringMeasure : IStringMeasure
    {
        private const int MaxMeasureTextCacheCount = 4096;

        private readonly Dictionary<IFontHandle, FontSystem> cacheFonts = new();
        private readonly Dictionary<(IFontHandle handle, FontStyle style), ResolvedTextStyle> cacheResolvedTextStyles = new();
        private readonly Dictionary<MeasureTextCacheKey, Vector2> cacheMeasureTextSizes = new();
        private readonly Queue<MeasureTextCacheKey> cacheMeasureTextSizeOrder = new();

        public static IEnumerable<IFontHandle> DefaultSupportFonts { get; } = GetSupportFonts();
        public static IFontHandle DefaultFont { get; } = DefaultSupportFonts.FirstOrDefault(x => x.FamilyName.ToLower() == "consola");

        public IEnumerable<IFontHandle> SupportFonts => DefaultSupportFonts;

        static DefaultStringMeasure()
        {
            FontSystemDefaults.FontLoader = new FreeTypeLoader();
        }

        public Vector2 MeasureString(string text, Vector2 scale, int fontSize, FontStyle style, IFontHandle handle)
        {
            text ??= string.Empty;
            handle ??= DefaultFont;

            var resolvedStyle = ResolveTextStyle(handle, style);
            var font = GetFontSystem(resolvedStyle.FontHandle).GetFont(fontSize);
            return MeasureString(font, text, resolvedStyle.FontHandle, fontSize, scale, resolvedStyle.FontStyle);
        }

        internal FontSystem GetFontSystem(IFontHandle fontHandle)
        {
            if (!cacheFonts.TryGetValue(fontHandle, out var fontSystem))
            {
                var settings = new FontSystemSettings
                {
                    FontResolutionFactor = 2,
                    KernelWidth = 1,
                    KernelHeight = 1,
                };
                fontSystem = new FontSystem(settings);

                if (ProgramSetting.Default.DisableStringRendererAntialiasing)
                {
                    var baseGlyphRenderer = settings.GlyphRenderer;
                    settings.GlyphRenderer = (i, o, opt) =>
                    {
                        baseGlyphRenderer(i, o, opt);
                        OnDisableAntialiasingGlyphRenderer(i, o, opt);
                    };
                }

                var handle = fontHandle as FontHandle;
                fontSystem.AddFont(File.ReadAllBytes(handle.FilePath));
                cacheFonts[fontHandle] = fontSystem;

                Log.LogDebug($"Created new FontSystem: {handle.FamilyName}, FilePath: {handle.FilePath}");
            }

            return fontSystem;
        }

        internal void RebuildFontSystem()
        {
            foreach (var fontSystem in cacheFonts.Values)
                fontSystem.Dispose();
            cacheFonts.Clear();
            ClearTextCaches();
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

        internal Vector2 MeasureString(DynamicSpriteFont font, string text, IFontHandle handle, int fontSize, Vector2 scale, TextStyle fontStyle)
        {
            var key = new MeasureTextCacheKey(text, handle, fontSize, scale, fontStyle);
            if (cacheMeasureTextSizes.TryGetValue(key, out var size))
                return size;

            size = font.MeasureString(text, scale);
            cacheMeasureTextSizes[key] = size;
            cacheMeasureTextSizeOrder.Enqueue(key);

            while (cacheMeasureTextSizes.Count > MaxMeasureTextCacheCount && cacheMeasureTextSizeOrder.TryDequeue(out var oldKey))
                cacheMeasureTextSizes.Remove(oldKey);

            return size;
        }

        private static IReadOnlyList<IFontHandle> GetSupportFonts()
        {
            return Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.Fonts)).Select(x => new FontHandle
            {
                FamilyName = Path.GetFileNameWithoutExtension(x),
                FilePath = x
            }).Where(x => Path.GetExtension(x.FilePath).Equals(".ttf", StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        private static void OnDisableAntialiasingGlyphRenderer(byte[] input, byte[] output, GlyphRenderOptions options)
        {
            var size = options.Size.X * options.Size.Y;

            for (var i = 0; i < size; i++)
            {
                var c = input[i];
                var ci = i * 4;

                if (c == 0)
                {
                    output[ci] = output[ci + 1] = output[ci + 2] = output[ci + 3] = 0;
                }
                else
                {
                    output[ci] = output[ci + 1] = output[ci + 2] = output[ci + 3] = 255;
                }
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
            public ResolvedTextStyle(IFontHandle fontHandle, TextStyle fontStyle)
            {
                FontHandle = fontHandle;
                FontStyle = fontStyle;
            }

            public IFontHandle FontHandle { get; }
            public TextStyle FontStyle { get; }
        }

        private sealed class FontHandle : IFontHandle
        {
            public string FamilyName { get; set; }
            public string FilePath { get; set; }
        }

        private readonly struct MeasureTextCacheKey : IEquatable<MeasureTextCacheKey>
        {
            public MeasureTextCacheKey(string text, IFontHandle fontHandle, int fontSize, Vector2 scale, TextStyle fontStyle)
            {
                Text = text;
                FontHandle = fontHandle;
                FontSize = fontSize;
                Scale = scale;
                FontStyle = fontStyle;
            }

            private string Text { get; }
            private IFontHandle FontHandle { get; }
            private int FontSize { get; }
            private Vector2 Scale { get; }
            private TextStyle FontStyle { get; }

            public bool Equals(MeasureTextCacheKey other)
            {
                return string.Equals(Text, other.Text, StringComparison.Ordinal)
                    && ReferenceEquals(FontHandle, other.FontHandle)
                    && FontSize == other.FontSize
                    && Scale.Equals(other.Scale)
                    && FontStyle == other.FontStyle;
            }

            public override bool Equals(object obj)
            {
                return obj is MeasureTextCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Text, FontHandle, FontSize, Scale, FontStyle);
            }
        }

        private void ClearTextCaches()
        {
            cacheResolvedTextStyles.Clear();
            cacheMeasureTextSizes.Clear();
            cacheMeasureTextSizeOrder.Clear();
        }
    }
}
