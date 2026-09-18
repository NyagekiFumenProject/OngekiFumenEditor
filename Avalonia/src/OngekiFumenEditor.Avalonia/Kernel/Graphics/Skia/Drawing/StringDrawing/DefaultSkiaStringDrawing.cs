using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using SixLabors.Fonts;
using SkiaSharp;
using System.Collections.Concurrent;
using System.Numerics;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.StringDrawing;

internal class DefaultSkiaStringDrawing : CommonSkiaDrawingBase, IStringDrawing, IStringMeasure, IDisposable
{
    private sealed class FontHandle : IStringDrawing.IFontHandle
    {
        public string FamilyName { get; init; }
        public string FilePath { get; init; }
    }

    private readonly record struct TypefaceKey(string FamilyName, bool Bold, bool Italic);

    private static IEnumerable<IStringDrawing.IFontHandle> defaultSupportFonts;

    // SKTypeface is immutable and safe to share across frames/threads. The key space is bounded by
    // the font families actually used times four style combinations, so the cache lives for the process.
    private static readonly ConcurrentDictionary<TypefaceKey, SKTypeface> typefaceCache = new();

    // Native-backed Skia objects are reused per instance: this class is created per command-list build
    // (as the measurer) and per replay (as the drawer), so both are single-threaded use sites.
    private readonly SKPaint textPaint = new();
    private readonly SKPaint decorationPaint = new();
    private readonly SKFont reusableFont = new();
    private bool disposed;

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

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(DefaultSkiaStringDrawing));
    }

    private SKFont ConfigureFont(int fontSize, IStringDrawing.StringStyle style, IStringDrawing.IFontHandle handle)
    {
        var isBold = style.HasFlag(IStringDrawing.StringStyle.Bold);
        var isItalic = style.HasFlag(IStringDrawing.StringStyle.Italic);
        var typefaceName = (handle ?? DefaultFont)?.FamilyName ?? SKTypeface.Default.FamilyName;

        reusableFont.Typeface = typefaceCache.GetOrAdd(
            new TypefaceKey(typefaceName ?? string.Empty, isBold, isItalic),
            static key => SKTypeface.FromFamilyName(
                key.FamilyName.Length == 0 ? null : key.FamilyName,
                key.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                key.Italic ? SKFontStyleSlant.Oblique : SKFontStyleSlant.Upright));
        reusableFont.Size = fontSize;
        return reusableFont;
    }

    public Vector2 MeasureString(
        string text,
        Vector2 scale,
        int fontSize,
        IStringDrawing.StringStyle style,
        IStringDrawing.IFontHandle handle)
    {
        ThrowIfDisposed();

        text ??= string.Empty;

        var font = ConfigureFont(fontSize, style, handle);
        font.MeasureText(System.Runtime.InteropServices.MemoryMarshal.Cast<char, ushort>(text.AsSpan()), out var bounds, textPaint);
        return new Vector2(bounds.Width * Math.Abs(scale.X), bounds.Height * Math.Abs(scale.Y));
    }

    public void Draw(string text, Vector2 pos, Vector2 scale, int fontSize, float rotate, Vector4 color, Vector2 origin, IStringDrawing.StringStyle style, IDrawingContext target, IStringDrawing.IFontHandle handle, out Vector2? measureTextSize)
    {
        ThrowIfDisposed();

        text ??= string.Empty;

        OnBegin(target);
        var canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;

        var isUnderline = style.HasFlag(IStringDrawing.StringStyle.Underline);
        var isStrike = style.HasFlag(IStringDrawing.StringStyle.Strike);

        var font = ConfigureFont(fontSize, style, handle);
        textPaint.ColorF = new SKColorF(color.X, color.Y, color.Z, color.W);

        font.MeasureText(System.Runtime.InteropServices.MemoryMarshal.Cast<char, ushort>(text.AsSpan()), out var bounds, textPaint);
        measureTextSize = new Vector2(bounds.Width, bounds.Height);

        var offsetPos = new SKPoint(origin.X * bounds.Width, bounds.Height - origin.Y * bounds.Height);
        var adjustPos = new SKPoint(pos.X - offsetPos.X, pos.Y - offsetPos.Y);

        canvas.Save();
        canvas.Translate(adjustPos.X, adjustPos.Y);
        if (Math.Abs(rotate) > float.Epsilon)
            canvas.RotateRadians(rotate);
        canvas.Scale(scale.X == 0 ? 1 : scale.X, scale.Y == 0 ? -1 : -scale.Y);
        canvas.DrawText(text, 0, 0, font, textPaint);
        target.PerfomenceMonitor.CountDrawCall();

        if (isUnderline || isStrike)
        {
            decorationPaint.Color = new SKColor((byte)(color.X * 255), (byte)(color.Y * 255), (byte)(color.Z * 255), (byte)(color.W * 255));
            font.GetFontMetrics(out var metrics);
            decorationPaint.StrokeWidth = metrics.UnderlineThickness ?? 2;

            if (isUnderline)
            {
                var underlineY = metrics.UnderlinePosition ?? 0;
                canvas.DrawLine(0, underlineY, bounds.Width, underlineY, decorationPaint);
            }
            if (isStrike)
            {
                var strikeY = -(metrics.XHeight / 2);
                canvas.DrawLine(0, strikeY, bounds.Width, strikeY, decorationPaint);
            }
            target.PerfomenceMonitor.CountDrawCall();
        }

        canvas.Restore();
        OnEnd();
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        reusableFont.Dispose();
        textPaint.Dispose();
        decorationPaint.Dispose();
    }
}
