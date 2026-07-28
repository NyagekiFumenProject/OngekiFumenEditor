using SixLabors.Fonts;
using SkiaSharp;
using System.Numerics;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.StringDrawing;

internal class DefaultSkiaStringDrawing : CommonSkiaDrawingBase, IStringDrawing, IDisposable
{
    private sealed class FontHandle : IStringDrawing.IFontHandle
    {
        public string FamilyName { get; init; }
        public string FilePath { get; init; }
    }

    private static IEnumerable<IStringDrawing.IFontHandle> defaultSupportFonts;

    public static IEnumerable<IStringDrawing.IFontHandle> DefaultSupportFonts => defaultSupportFonts ??= GetSupportFonts();

    public IEnumerable<IStringDrawing.IFontHandle> SupportFonts => DefaultSupportFonts;

    public static IStringDrawing.IFontHandle DefaultFont => DefaultSupportFonts.FirstOrDefault(x => x.FamilyName.Equals("Consolas", StringComparison.OrdinalIgnoreCase));

    public DefaultSkiaStringDrawing(DefaultSkiaDrawingManagerImpl manager) : base(manager)
    {
    }

    private static IEnumerable<IStringDrawing.IFontHandle> GetSupportFonts()
    {
        return SystemFonts.Collection.Families
            .Select(x =>
            {
                if (!x.TryGetPaths(out var paths))
                    return null;

                return new FontHandle
                {
                    FamilyName = x.Name,
                    FilePath = paths.FirstOrDefault(path => Path.GetExtension(path).Equals(".ttf", StringComparison.OrdinalIgnoreCase))
                };
            })
            .Where(x => x?.FilePath is not null)
            .ToArray();
    }

    public void Draw(string text, Vector2 pos, Vector2 scale, int fontSize, float rotate, Vector4 color, Vector2 origin, IStringDrawing.StringStyle style, IDrawingContext target, IStringDrawing.IFontHandle handle, out Vector2? measureTextSize)
    {
        text ??= string.Empty;

        OnBegin(target);
        var canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;

        using var paint = new SKPaint { ColorF = new(color.X, color.Y, color.Z, color.W) };
        using var font = new SKFont();

        var isBold = style.HasFlag(IStringDrawing.StringStyle.Bold);
        var isItalic = style.HasFlag(IStringDrawing.StringStyle.Italic);
        var isUnderline = style.HasFlag(IStringDrawing.StringStyle.Underline);
        var isStrike = style.HasFlag(IStringDrawing.StringStyle.Strike);

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

        var offsetPos = new SKPoint(origin.X * bounds.Width, bounds.Height - origin.Y * bounds.Height);
        var adjustPos = new SKPoint(pos.X - offsetPos.X, pos.Y - offsetPos.Y);

        canvas.Save();
        canvas.Translate(adjustPos.X, adjustPos.Y);
        if (Math.Abs(rotate) > float.Epsilon)
            canvas.RotateRadians(rotate);
        canvas.Scale(scale.X == 0 ? 1 : scale.X, scale.Y == 0 ? 1 : scale.Y);
        canvas.DrawText(text, 0, 0, font, paint);
        target.PerfomenceMonitor.CountDrawCall(this);

        if (isUnderline || isStrike)
        {
            using var linePaint = new SKPaint
            {
                Color = new SKColor((byte)(color.X * 255), (byte)(color.Y * 255), (byte)(color.Z * 255), (byte)(color.W * 255))
            };
            font.GetFontMetrics(out var metrics);
            linePaint.StrokeWidth = metrics.UnderlineThickness ?? 2;

            if (isUnderline)
            {
                var underlineY = metrics.UnderlinePosition ?? 0;
                canvas.DrawLine(0, underlineY, bounds.Width, underlineY, linePaint);
            }
            if (isStrike)
            {
                var strikeY = -(metrics.XHeight / 2);
                canvas.DrawLine(0, strikeY, bounds.Width, strikeY, linePaint);
            }
            target.PerfomenceMonitor.CountDrawCall(this);
        }

        canvas.Restore();
        OnEnd();
    }

    public void Dispose()
    {
    }
}