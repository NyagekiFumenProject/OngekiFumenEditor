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

        private static IEnumerable<IFontHandle> defaultSupportFonts;
        public static IEnumerable<IFontHandle> DefaultSupportFonts { get; } = GetSupportFonts();
        public IEnumerable<IFontHandle> SupportFonts => DefaultSupportFonts;
        public static IFontHandle DefaultFont { get; } = GetSupportFonts().FirstOrDefault(x => x.FamilyName.ToLower() == "consola");

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

        public Vector2 MeasureString(string text, Vector2 scale, int fontSize, FontStyle style, IFontHandle handle)
        {
            text ??= string.Empty;

            using var paint = new SKPaint();
            paint.IsAntialias = !ProgramSetting.Default.DisableStringRendererAntialiasing;

            using var font = new SKFont();

            var isBold = style.HasFlag(FontStyle.Bold);
            var isItalic = style.HasFlag(FontStyle.Italic);
            var typefaceName = (handle ?? DefaultFont)?.FamilyName ?? SKTypeface.Default.FamilyName;

            using var typeface = SKTypeface.FromFamilyName(
                   typefaceName,
                   isBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                   SKFontStyleWidth.Normal,
                    isItalic ? SKFontStyleSlant.Oblique : SKFontStyleSlant.Upright);

            font.Typeface = typeface;
            font.Size = fontSize;
            font.Edging = paint.IsAntialias ? SKFontEdging.SubpixelAntialias : SKFontEdging.Alias;
            font.Hinting = SKFontHinting.Full;
            font.Subpixel = true;

            font.MeasureText(text, out var bounds, paint);
            return new Vector2(bounds.Width * Math.Abs(scale.X), bounds.Height * Math.Abs(scale.Y));
        }

        public void Draw(string text, Vector2 pos, Vector2 scale, int fontSize, float rotate, Vector4 color, Vector2 origin, FontStyle style, IDrawingContext target, IFontHandle handle, out Vector2? measureTextSize)
        {
            text = text ?? string.Empty;

            OnBegin(target);
            var canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;

            using var paint = new SKPaint();
            paint.IsAntialias = !ProgramSetting.Default.DisableStringRendererAntialiasing;
            paint.ColorF = new(color.X, color.Y, color.Z, color.W);

            using var font = new SKFont();

            var isBold = style.HasFlag(FontStyle.Bold);
            var isItalic = style.HasFlag(FontStyle.Italic);
            var isUnderline = style.HasFlag(FontStyle.Underline);
            var isStrike = style.HasFlag(FontStyle.Strike);

            var typefaceName = (handle ?? DefaultFont)?.FamilyName ?? SKTypeface.Default.FamilyName;

            using var typeface = SKTypeface.FromFamilyName(
                   typefaceName,
                   isBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                   SKFontStyleWidth.Normal,
                    isItalic ? SKFontStyleSlant.Oblique : SKFontStyleSlant.Upright);

            font.Typeface = typeface;
            font.Size = fontSize;
            font.Edging = paint.IsAntialias ? SKFontEdging.SubpixelAntialias : SKFontEdging.Alias;
            font.Hinting = SKFontHinting.Full;
            font.Subpixel = true;

            font.MeasureText(text, out var bounds, paint);
            measureTextSize = new Vector2(bounds.Width * Math.Abs(scale.X), bounds.Height * Math.Abs(scale.Y));
            //adjust pos thought origin and size

            var offsetPos = new SKPoint(origin.X * bounds.Width, bounds.Height - origin.Y * bounds.Height);

            canvas.Scale(1, -1);
            canvas.Translate(pos.X, -pos.Y);
            if (rotate != 0)
                canvas.RotateDegrees(rotate * 180f / MathF.PI);
            canvas.Scale(scale.X, scale.Y);

            var adjustPos = new SKPoint(-offsetPos.X, offsetPos.Y);
            canvas.DrawText(text, adjustPos, font, paint);
            target.RenderContext.PerfomenceMonitor.CountDrawCall();

            if (isUnderline || isStrike)
            {
                using var linePaint = new SKPaint();
                linePaint.IsAntialias = paint.IsAntialias;
                linePaint.Color = new SKColor((byte)(color.X * 255), (byte)(color.Y * 255), (byte)(color.Z * 255), (byte)(color.W * 255));
                font.GetFontMetrics(out var metrics);
                linePaint.StrokeWidth = metrics.UnderlineThickness ?? 2;

                if (isUnderline)
                {
                    var underlineY = adjustPos.Y + metrics.UnderlinePosition ?? 0;
                    canvas.DrawLine(adjustPos.X, underlineY, adjustPos.X + bounds.Width, underlineY, linePaint);
                    target.RenderContext.PerfomenceMonitor.CountDrawCall();
                }
                else
                {
                    float strikeY = adjustPos.Y - metrics.XHeight / 2;
                    canvas.DrawLine(adjustPos.X, strikeY, adjustPos.X + bounds.Width, strikeY, linePaint);
                    target.RenderContext.PerfomenceMonitor.CountDrawCall();
                }
            }

            typeface?.Dispose();
            OnEnd();
        }

        public void Dispose()
        {

        }
    }
}
