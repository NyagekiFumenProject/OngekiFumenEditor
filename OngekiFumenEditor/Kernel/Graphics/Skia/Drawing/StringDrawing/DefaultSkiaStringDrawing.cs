using FontStashSharp;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Kernel.Graphics.Text;
using OngekiFumenEditor.Utils;
using SharpVectors.Dom.Svg;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.StringDrawing
{
    internal sealed class DefaultSkiaStringDrawing : CommonSkiaDrawingBase, IStringDrawing, IStringMeasure, IDisposable
    {
        private class FontHandle : IFontHandle
        {
            public string FamilyName { get; set; }
            public string FilePath { get; set; }
        }

        private readonly SkiaFontFallback fontFallback = new();
        private readonly Dictionary<(string family, bool bold, bool italic), SKTypeface> typefaces = new();
        private readonly SKPaint antialiasedPaint = new() { IsAntialias = true };
        private readonly SKPaint aliasedPaint = new() { IsAntialias = false };
        private readonly SKPaint linePaint = new();

        private static IEnumerable<IFontHandle> defaultSupportFonts;
        public static IEnumerable<IFontHandle> DefaultSupportFonts { get; } = GetSupportFonts();
        public IEnumerable<IFontHandle> SupportFonts => DefaultSupportFonts;
        public static IFontHandle DefaultFont { get; } = ResolveDefaultFont();

        /// <summary>
        /// 解析默认字体：<see cref="ProgramSetting.EditorFontFamilyName"/> 非空时按家族名匹配系统字体表，
        /// 未命中则把配置名直接交给 <c>SKTypeface.FromFamilyName</c>（由绘制侧再退回 <c>SKTypeface.Default</c>）；
        /// 为空时保持既有 "consola" 匹配行为（系统字体家族名通常为 "Consolas"，故结果为 null → <c>SKTypeface.Default</c>）。
        /// </summary>
        private static IFontHandle ResolveDefaultFont()
        {
            var configured = ProgramSetting.Default.EditorFontFamilyName;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return DefaultSupportFonts.FirstOrDefault(x => string.Equals(x.FamilyName, configured, StringComparison.OrdinalIgnoreCase))
                    ?? new FontHandle { FamilyName = configured };
            }

            return DefaultSupportFonts.FirstOrDefault(x => x.FamilyName.ToLower() == "consola");
        }

        public DefaultSkiaStringDrawing(DefaultSkiaDrawingManagerImpl manager) : base(manager)
        {

        }

        private static IEnumerable<IFontHandle> GetSupportFonts()
        {
            if (defaultSupportFonts != null)
                return defaultSupportFonts;

            return defaultSupportFonts = SixLabors.Fonts.SystemFonts.Collection.Families.Select(x =>
            {
                if (!x.TryGetPaths(out var paths))
                    return default;
                var handle = new FontHandle()
                {
                    FamilyName = x.Name,
                    FilePath = paths.FirstOrDefault(path => Path.GetExtension(path).ToLower() == ".ttf")
                };
                return handle;
            }).Where(x => x?.FilePath != null)
            .ToArray();
        }

        private SKTypeface GetTypeface(IFontHandle handle, FontStyle style)
        {
            var family = (handle ?? DefaultFont)?.FamilyName ?? SKTypeface.Default.FamilyName;
            var bold = style.HasFlag(FontStyle.Bold);
            var italic = style.HasFlag(FontStyle.Italic);
            var key = (family, bold, italic);
            if (typefaces.TryGetValue(key, out var typeface))
                return typeface;

            typeface = SKTypeface.FromFamilyName(family,
                bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                italic ? SKFontStyleSlant.Oblique : SKFontStyleSlant.Upright) ?? SKTypeface.Default;
            typefaces[key] = typeface;
            return typeface;
        }

        public Vector2 MeasureString(string text, Vector2 scale, int fontSize, FontStyle style, IFontHandle handle)
        {
            text ??= string.Empty;
            if (scale.X == 0 || scale.Y == 0)
                return Vector2.Zero;
            var aliased = ProgramSetting.Default.DisableStringRendererAntialiasing;
            fontFallback.MeasureText(GetTypeface(handle, style), text, fontSize, aliased,
                aliased ? aliasedPaint : antialiasedPaint, out var bounds, true);
            return new Vector2(bounds.Width * Math.Abs(scale.X), bounds.Height * Math.Abs(scale.Y));
        }

        public void Draw(string text, Vector2 pos, Vector2 scale, int fontSize, float rotate, Vector4 color, Vector2 origin, FontStyle style, IDrawingContext target, IFontHandle handle, out Vector2? measureTextSize)
        {
            text = text ?? string.Empty;

            if (scale.X == 0 || scale.Y == 0)
            {
                measureTextSize = Vector2.Zero;
                return;
            }

            if (!OnBegin(target))
            {
                measureTextSize = default;
                return;
            }

            var canvas = Canvas;

            var aliased = ProgramSetting.Default.DisableStringRendererAntialiasing;
            var paint = aliased ? aliasedPaint : antialiasedPaint;
            paint.ColorF = new(color.X, color.Y, color.Z, color.W);
            var isUnderline = style.HasFlag(FontStyle.Underline);
            var isStrike = style.HasFlag(FontStyle.Strike);

            var typeface = GetTypeface(handle, style);
            fontFallback.MeasureText(typeface, text, fontSize, aliased, paint, out var bounds, true);
            measureTextSize = new Vector2(bounds.Width * Math.Abs(scale.X), bounds.Height * Math.Abs(scale.Y));
            //adjust pos thought origin and size

            var offsetPos = new SKPoint(bounds.Left + origin.X * bounds.Width, -bounds.Top - origin.Y * bounds.Height);

            canvas.Scale(1, -1);
            canvas.Translate(pos.X, -pos.Y);
            if (rotate != 0)
                canvas.RotateDegrees(rotate * 180f / MathF.PI);
            canvas.Scale(scale.X, scale.Y);

            var adjustPos = new SKPoint(-offsetPos.X, offsetPos.Y);
            var penX = adjustPos.X;
            foreach (var run in fontFallback.GetRuns(typeface, text))
            {
                var font = fontFallback.GetFont(run.Typeface, fontSize, aliased, true);
                canvas.DrawText(run.Text, penX, adjustPos.Y, font, paint);
                penX += font.MeasureText(run.Text, paint);
                target.RenderContext.PerfomenceMonitor.CountDrawCall();
            }

            if (isUnderline || isStrike)
            {
                linePaint.IsAntialias = paint.IsAntialias;
                linePaint.Color = new SKColor((byte)(color.X * 255), (byte)(color.Y * 255), (byte)(color.Z * 255), (byte)(color.W * 255));
                var font = fontFallback.GetFont(typeface, fontSize, aliased, true);
                font.GetFontMetrics(out var metrics);
                linePaint.StrokeWidth = metrics.UnderlineThickness ?? 2;

                if (isUnderline)
                {
                    var underlineY = adjustPos.Y + metrics.UnderlinePosition ?? 0;
                    canvas.DrawLine(adjustPos.X + bounds.Left, underlineY, adjustPos.X + bounds.Right, underlineY, linePaint);
                    target.RenderContext.PerfomenceMonitor.CountDrawCall();
                }
                else
                {
                    float strikeY = adjustPos.Y - metrics.XHeight / 2;
                    canvas.DrawLine(adjustPos.X + bounds.Left, strikeY, adjustPos.X + bounds.Right, strikeY, linePaint);
                    target.RenderContext.PerfomenceMonitor.CountDrawCall();
                }
            }

            OnEnd();
        }

        public void Dispose()
        {
            fontFallback.Dispose();
            foreach (var typeface in typefaces.Values)
                if (!ReferenceEquals(typeface, SKTypeface.Default))
                    typeface.Dispose();
            typefaces.Clear();
            antialiasedPaint.Dispose();
            aliasedPaint.Dispose();
            linePaint.Dispose();
        }
    }
}
