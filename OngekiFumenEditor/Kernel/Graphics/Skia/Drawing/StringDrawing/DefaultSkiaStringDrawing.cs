using FontStashSharp;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using SharpVectors.Dom.Svg;
using SixLabors.Fonts;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Numerics;
using static OngekiFumenEditor.Kernel.Graphics.IStringDrawing;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.StringDrawing
{
    internal class DefaultSkiaStringDrawing : CommonSkiaDrawingBase, IStringDrawing, IDisposable
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

        public DefaultSkiaStringDrawing(DefaultSkiaDrawingManager manager) : base(manager)
        {

        }

        private static IEnumerable<IFontHandle> GetSupportFonts()
        {
            if (defaultSupportFonts != null)
                return defaultSupportFonts;

            return defaultSupportFonts = SystemFonts.Collection.Families.Select(x =>
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

        public void Draw(string text, Vector2 pos, Vector2 scale, int fontSize, float rotate, Vector4 color, Vector2 origin, StringStyle style, IDrawingContext target, IStringDrawing.IFontHandle handle, out Vector2? measureTextSize)
        {
            text = text ?? string.Empty;

            OnBegin(target);
            var canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;

            canvas.Scale(1, -1);

            using var paint = new SKPaint();
            paint.ColorF = new(color.X, color.Y, color.Z, color.W);

            using var font = new SKFont();

            var isBold = style.HasFlag(StringStyle.Bold);
            var isItalic = style.HasFlag(StringStyle.Italic);
            var isUnderline = style.HasFlag(StringStyle.Underline);
            var isStrike = style.HasFlag(StringStyle.Strike);

            var typefaceName = (handle ?? DefaultFont)?.FamilyName ?? SKTypeface.Default.FamilyName;

            using var typeface = SKTypeface.FromFamilyName(
                   typefaceName,
                   isBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                   SKFontStyleWidth.Normal,
                    isItalic ? SKFontStyleSlant.Oblique : SKFontStyleSlant.Upright);

            font.Typeface = typeface;
            font.Size = fontSize;

            font.MeasureText(text, out var bounds, paint);
            measureTextSize = new Vector2(bounds.Width, bounds.Height);
            //adjust pos thought origin and size

            var offsetPos = new SKPoint(origin.X * bounds.Width, origin.Y * bounds.Height);

            var adjustPos = pos.ToSkiaSharpPoint() - offsetPos;
            adjustPos.Y = -adjustPos.Y;

            canvas.DrawText(text, adjustPos, font, paint);
            target.PerfomenceMonitor.CountDrawCall(this);

            if (isUnderline || isStrike)
            {
                using var linePaint = new SKPaint();
                linePaint.Color = new SKColor((byte)(color.X * 255), (byte)(color.Y * 255), (byte)(color.Z * 255), (byte)(color.W * 255));
                font.GetFontMetrics(out var metrics);
                linePaint.StrokeWidth = metrics.UnderlineThickness ?? 2;

                if (isUnderline)
                {
                    var underlineY = adjustPos.Y + metrics.UnderlinePosition ?? 0;
                    canvas.DrawLine(adjustPos.X, underlineY, adjustPos.X + bounds.Width, underlineY, linePaint);
                    target.PerfomenceMonitor.CountDrawCall(this);
                }
                else
                {
                    float strikeY = adjustPos.Y - metrics.XHeight / 2;
                    canvas.DrawLine(adjustPos.X, strikeY, adjustPos.X + bounds.Width, strikeY, linePaint);
                    target.PerfomenceMonitor.CountDrawCall(this);
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
