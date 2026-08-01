#nullable enable

using System.ComponentModel;
using Avalonia.Media;
using OngekiFumenEditor.Avalonia.Base.Attributes;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater;
using OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater.OgkrImpl.Factory;
using SkiaSharp;
using Svg.Skia;
using DrawPathCanvasCommand = ShimSkiaSharp.DrawPathCanvasCommand;
using DrawPictureCanvasCommand = ShimSkiaSharp.DrawPictureCanvasCommand;
using DrawPositionedTextRunCanvasCommand = ShimSkiaSharp.DrawPositionedTextRunCanvasCommand;
using DrawTextBlobCanvasCommand = ShimSkiaSharp.DrawTextBlobCanvasCommand;
using DrawTextCanvasCommand = ShimSkiaSharp.DrawTextCanvasCommand;
using DrawTextOnPathCanvasCommand = ShimSkiaSharp.DrawTextOnPathCanvasCommand;
using RestoreCanvasCommand = ShimSkiaSharp.RestoreCanvasCommand;
using SaveCanvasCommand = ShimSkiaSharp.SaveCanvasCommand;
using SaveLayerCanvasCommand = ShimSkiaSharp.SaveLayerCanvasCommand;
using SetMatrixCanvasCommand = ShimSkiaSharp.SetMatrixCanvasCommand;
using ShimMatrix = ShimSkiaSharp.SKMatrix;
using ShimPaint = ShimSkiaSharp.SKPaint;
using ShimPicture = ShimSkiaSharp.SKPicture;

namespace OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;

public abstract class SvgPrefabBase : OngekiMovableObjectBase
{
    private const int MaximumRasterDimension = 2048;
    private ICurveInterpolaterFactory curveInterpolaterFactory = XGridLimitedCurveInterpolaterFactory.Default;
    private bool isForceColorful;
    private ColorId colorfulLaneColor = ColorIdConst.Yuzu;
    private RangeValue colorfulLaneBrightness = RangeValue.Create(-3, 3, 0);
    private RangeValue rotation = RangeValue.Create(-180, 180f, 0f);
    private RangeValue offsetX = RangeValue.CreateNormalized(0.5f);
    private RangeValue colorSimilar = RangeValue.Create(1, 1000, 600);
    private RangeValue offsetY = RangeValue.CreateNormalized(0.5f);
    private bool enableColorfulLaneSimilar = true;
    private bool showOriginColor;
    private float scale = 1;
    private RangeValue opacity = RangeValue.CreateNormalized(1);
    private RangeValue tolerance = RangeValue.Create(0.001f, 20f, 20f);
    private SKSvg? svg;
    private SKBitmap? processingBitmap;

    public ICurveInterpolaterFactory CurveInterpolaterFactory
    {
        get => curveInterpolaterFactory;
        set => SetProperty(ref curveInterpolaterFactory, value ?? XGridLimitedCurveInterpolaterFactory.Default);
    }

    public bool IsForceColorful
    {
        get => isForceColorful;
        set => SetProperty(ref isForceColorful, value);
    }

    public ColorId ColorfulLaneColor
    {
        get => colorfulLaneColor;
        set => SetProperty(ref colorfulLaneColor, value);
    }

    [ObjectPropertyBrowserSingleSelectedOnly]
    public RangeValue ColorfulLaneBrightness
    {
        get => colorfulLaneBrightness;
        set => SetRangeValue(ref colorfulLaneBrightness, value, nameof(ColorfulLaneBrightness));
    }

    [ObjectPropertyBrowserSingleSelectedOnly]
    public RangeValue Rotation
    {
        get => rotation;
        set => SetRangeValue(ref rotation, value, nameof(Rotation));
    }

    [ObjectPropertyBrowserSingleSelectedOnly]
    public RangeValue OffsetX
    {
        get => offsetX;
        set => SetRangeValue(ref offsetX, value, nameof(OffsetX));
    }

    [ObjectPropertyBrowserSingleSelectedOnly]
    public RangeValue ColorSimilar
    {
        get => colorSimilar;
        set => SetRangeValue(ref colorSimilar, value, nameof(ColorSimilar));
    }

    [ObjectPropertyBrowserSingleSelectedOnly]
    public RangeValue OffsetY
    {
        get => offsetY;
        set => SetRangeValue(ref offsetY, value, nameof(OffsetY));
    }

    public bool EnableColorfulLaneSimilar
    {
        get => enableColorfulLaneSimilar;
        set => SetProperty(ref enableColorfulLaneSimilar, value);
    }

    public bool ShowOriginColor
    {
        get => showOriginColor;
        set => SetProperty(ref showOriginColor, value);
    }

    public float Scale
    {
        get => scale;
        set => SetProperty(ref scale, value);
    }

    [ObjectPropertyBrowserSingleSelectedOnly]
    public RangeValue Opacity
    {
        get => opacity;
        set => SetRangeValue(ref opacity, value, nameof(Opacity));
    }

    [ObjectPropertyBrowserSingleSelectedOnly]
    public RangeValue Tolerance
    {
        get => tolerance;
        set => SetRangeValue(ref tolerance, value, nameof(Tolerance));
    }

    [ObjectPropertyBrowserHide]
    public SKPicture? Picture => svg?.Picture;

    [ObjectPropertyBrowserHide]
    public SKBitmap? ProcessingBitmap => processingBitmap;

    [ObjectPropertyBrowserHide]
    public SKRect SourceBounds => svg?.Picture?.CullRect ?? SKRect.Empty;

    protected SvgPrefabBase()
    {
        AttachRangeValues();
    }

    public override void Copy(OngekiObjectBase fromObj)
    {
        base.Copy(fromObj);
        if (fromObj is not SvgPrefabBase from)
            return;

        // The base movable-object implementation retains grid references. SVG
        // prefabs are independently editable, so keep their copies independent.
        TGrid = from.TGrid.CopyNew();
        XGrid = from.XGrid.CopyNew();
        Tolerance = Clone(from.Tolerance);
        Opacity = Clone(from.Opacity);
        Rotation = Clone(from.Rotation);
        OffsetX = Clone(from.OffsetX);
        OffsetY = Clone(from.OffsetY);
        ColorSimilar = Clone(from.ColorSimilar);
        ColorfulLaneBrightness = Clone(from.ColorfulLaneBrightness);
        ShowOriginColor = from.ShowOriginColor;
        IsForceColorful = from.IsForceColorful;
        CurveInterpolaterFactory = from.CurveInterpolaterFactory;
        ColorfulLaneColor = from.ColorfulLaneColor;
        EnableColorfulLaneSimilar = from.EnableColorfulLaneSimilar;
        Scale = from.Scale;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        switch (e.PropertyName)
        {
            case nameof(EnableColorfulLaneSimilar):
            case nameof(ShowOriginColor):
            case nameof(IsForceColorful):
            case nameof(ColorfulLaneColor):
            case nameof(Opacity):
            case nameof(ColorSimilar):
            case nameof(RangeValue.CurrentValue):
                RebuildRendering();
                break;
        }
    }

    protected void ApplySvgContent(string svgContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(svgContent);

        var next = new SKSvg();
        try
        {
            if (next.FromSvg(svgContent) is null)
                throw new FormatException("SVG content did not produce a Skia picture.");
        }
        catch
        {
            next.Dispose();
            throw;
        }

        ReplaceSvg(next);
    }

    protected void ApplySvgContent(Stream svgContent)
    {
        ArgumentNullException.ThrowIfNull(svgContent);

        var next = new SKSvg();
        try
        {
            if (next.Load(svgContent) is null)
                throw new FormatException("SVG content did not produce a Skia picture.");
        }
        catch
        {
            next.Dispose();
            throw;
        }

        ReplaceSvg(next);
    }

    public void CleanGeometry()
    {
        processingBitmap?.Dispose();
        processingBitmap = null;
        svg?.Dispose();
        svg = null;
        OnPropertyChanged(nameof(Picture));
        OnPropertyChanged(nameof(ProcessingBitmap));
        OnPropertyChanged(nameof(SourceBounds));
    }

    public void RebuildGeometry() => RebuildRendering();

    public LaneColor? PickSimilarLaneColor(Color color)
    {
        var candidates = LaneColor.AllLaneColors.Where(x => x.LaneType is not (LaneType.WallRight or LaneType.WallLeft));
        if (!EnableColorfulLaneSimilar)
            candidates = candidates.Where(x => x.LaneType != LaneType.Colorful);

        var match = candidates
            .Select(x => (LaneColor: x, Distance: ColorDistance(x.Color, color)))
            .OrderBy(x => x.Distance)
            .FirstOrDefault();

        return match.Distance < ColorSimilar.CurrentValue ? match.LaneColor : null;
    }

    public sealed class LineSegment
    {
        public List<System.Numerics.Vector2> RelativePoints { get; } = [];
        public Color Color { get; init; }
    }

    public List<LineSegment> GenerateLineSegments()
    {
        var outputSegments = new List<LineSegment>();
        if (svg?.Model is not { } model)
            return outputSegments;

        VisitPicture(model, ShimMatrix.CreateIdentity(), outputSegments);
        return outputSegments;
    }

    public override string ToString() => $"{base.ToString()} R[∠{Rotation.CurrentValue}°] O[{Opacity.ValuePercent * 100:F2}%] S[{Scale:F2}x]";

    public override void Dispose()
    {
        DetachRangeValues();
        CleanGeometry();
        base.Dispose();
    }

    private void ReplaceSvg(SKSvg next)
    {
        processingBitmap?.Dispose();
        processingBitmap = null;
        svg?.Dispose();
        svg = next;
        OnPropertyChanged(nameof(Picture));
        OnPropertyChanged(nameof(SourceBounds));
        RebuildRendering();
    }

    private void RebuildRendering()
    {
        processingBitmap?.Dispose();
        processingBitmap = null;

        var picture = svg?.Picture;
        if (picture is null)
        {
            OnPropertyChanged(nameof(ProcessingBitmap));
            return;
        }

        var bounds = picture.CullRect;
        if (!float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            OnPropertyChanged(nameof(ProcessingBitmap));
            return;
        }

        var rasterScale = Math.Min(1f, MaximumRasterDimension / Math.Max(bounds.Width, bounds.Height));
        var width = Math.Max(1, (int)Math.Ceiling(bounds.Width * rasterScale));
        var height = Math.Max(1, (int)Math.Ceiling(bounds.Height * rasterScale));
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(rasterScale);
            canvas.Translate(-bounds.Left, -bounds.Top);
            canvas.DrawPicture(picture);
            canvas.Flush();
        }

        ProcessPixels(bitmap);
        processingBitmap = bitmap;
        OnPropertyChanged(nameof(ProcessingBitmap));
    }

    private void ProcessPixels(SKBitmap bitmap)
    {
        var opacityFactor = Math.Clamp(Opacity.CurrentValue, 0, 1);
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var source = bitmap.GetPixel(x, y);
                if (source.Alpha == 0)
                    continue;

                var alpha = (byte)Math.Clamp((int)Math.Round(source.Alpha * opacityFactor), 0, byte.MaxValue);
                if (ShowOriginColor)
                {
                    bitmap.SetPixel(x, y, source.WithAlpha(alpha));
                    continue;
                }

                var output = IsForceColorful
                    ? ColorfulLaneColor.Color
                    : PickSimilarLaneColor(Color.FromArgb(source.Alpha, source.Red, source.Green, source.Blue))?.Color;
                bitmap.SetPixel(x, y, output is { } color
                    ? new SKColor(color.R, color.G, color.B, alpha)
                    : SKColors.Transparent);
            }
        }
    }

    private void VisitPicture(ShimPicture picture, ShimMatrix parentMatrix, List<LineSegment> outputSegments)
    {
        if (picture.Commands is null)
            return;

        var localMatrix = ShimMatrix.CreateIdentity();
        var matrixStack = new Stack<ShimMatrix>();
        foreach (var command in picture.Commands)
        {
            switch (command)
            {
                case SaveCanvasCommand:
                case SaveLayerCanvasCommand:
                    matrixStack.Push(localMatrix);
                    break;
                case RestoreCanvasCommand:
                    if (matrixStack.Count > 0)
                        localMatrix = matrixStack.Pop();
                    break;
                case SetMatrixCanvasCommand setMatrix:
                    localMatrix = setMatrix.TotalMatrix;
                    break;
                case DrawPictureCanvasCommand { Picture: { } nested }:
                    VisitPicture(nested, parentMatrix * localMatrix, outputSegments);
                    break;
                case DrawPathCanvasCommand { Path: { } path } drawPath:
                    using (var skPath = svg!.SkiaModel.ToSKPath(path))
                        AppendPathSegments(skPath, drawPath.Paint, parentMatrix * localMatrix, outputSegments);
                    break;
                case DrawTextCanvasCommand drawText:
                    using (var textPath = CreateTextPath(drawText.Text, drawText.X, drawText.Y, drawText.Font, drawText.Paint))
                        AppendPathSegments(textPath, drawText.Paint, parentMatrix * localMatrix, outputSegments);
                    break;
                case DrawTextBlobCanvasCommand drawTextBlob:
                    using (var textPath = CreateTextPath(
                               drawTextBlob.TextBlob?.Text ?? string.Empty,
                               drawTextBlob.X,
                               drawTextBlob.Y,
                               drawTextBlob.TextBlob?.Font,
                               drawTextBlob.Paint))
                        AppendPathSegments(textPath, drawTextBlob.Paint, parentMatrix * localMatrix, outputSegments);
                    break;
                case DrawPositionedTextRunCanvasCommand positionedText:
                    AppendPositionedTextSegments(positionedText, parentMatrix * localMatrix, outputSegments);
                    break;
                case DrawTextOnPathCanvasCommand textOnPath when textOnPath.Path is { } sourcePath:
                    using (var guidePath = svg!.SkiaModel.ToSKPath(sourcePath))
                    using (var font = CreateFont(textOnPath.Font, textOnPath.Paint))
                    using (var textPath = font.GetTextPathOnPath(
                               textOnPath.Text,
                               guidePath,
                               ConvertTextAlign(textOnPath.TextAlign),
                               new SKPoint(textOnPath.HOffset, textOnPath.VOffset)))
                        AppendPathSegments(textPath, textOnPath.Paint, parentMatrix * localMatrix, outputSegments);
                    break;
            }
        }
    }

    private void AppendPositionedTextSegments(
        DrawPositionedTextRunCanvasCommand command,
        ShimMatrix matrix,
        List<LineSegment> outputSegments)
    {
        if (command.Fragments is null)
            return;

        foreach (var fragment in command.Fragments)
        {
            using var path = CreateTextPath(fragment.Text, fragment.Point.X, fragment.Point.Y, command.Font, command.Paint);
            var fragmentMatrix = ShimMatrix.CreateScale(fragment.ScaleX, 1, fragment.ScaleOriginX, fragment.Point.Y)
                .PostConcat(ShimMatrix.CreateRotationDegrees(fragment.RotationDegrees, fragment.Point.X, fragment.Point.Y));
            AppendPathSegments(path, command.Paint, matrix * fragmentMatrix, outputSegments);
        }
    }

    private static SKPath CreateTextPath(string text, float x, float y, ShimSkiaSharp.SKFont? sourceFont, ShimPaint? paint)
    {
        using var font = CreateFont(sourceFont, paint);
        return font.GetTextPath(text ?? string.Empty, new SKPoint(x, y));
    }

    private static SKFont CreateFont(ShimSkiaSharp.SKFont? sourceFont, ShimPaint? paint)
    {
        var familyName = sourceFont?.Typeface?.FamilyName ?? paint?.Typeface?.FamilyName;
        var typeface = SKTypeface.FromFamilyName(familyName) ?? SKTypeface.Default;
        var size = sourceFont?.Size ?? paint?.TextSize ?? 16;
        return new SKFont(typeface, Math.Max(0.001f, size))
        {
            ScaleX = sourceFont?.ScaleX ?? 1,
            SkewX = sourceFont?.SkewX ?? 0,
            Subpixel = sourceFont?.Subpixel ?? true,
            Embolden = sourceFont?.Embolden ?? false
        };
    }

    private void AppendPathSegments(
        SKPath path,
        ShimPaint? paint,
        ShimMatrix matrix,
        List<LineSegment> outputSegments)
    {
        if (path.IsEmpty)
            return;

        using var transformedPath = new SKPath(path);
        transformedPath.Transform(ToSkiaMatrix(matrix));
        var color = ResolveSegmentColor(paint);
        if (color is null)
            return;

        foreach (var points in FlattenPath(transformedPath))
        {
            if (points.Count < 2)
                continue;

            var segment = new LineSegment { Color = color.Value };
            foreach (var point in points)
            {
                var relativePoint = CalculateRelativePoint(point);
                //Log.LogDebug($"{point}  ->  {relativePoint}");
                segment.RelativePoints.Add(relativePoint);
            }

            //append to list
            outputSegments.Add(segment);
        }
    }

    private IEnumerable<List<SKPoint>> FlattenPath(SKPath path)
    {
        using var iterator = path.CreateRawIterator();
        var points = new SKPoint[4];
        List<SKPoint>? contour = null;
        SKPoint contourStart = default;
        while (true)
        {
            var verb = iterator.Next(points);
            switch (verb)
            {
                case SKPathVerb.Move:
                    if (contour is { Count: > 1 })
                        yield return contour;
                    contour = [points[0]];
                    contourStart = points[0];
                    break;
                case SKPathVerb.Line:
                    contour ??= [points[0]];
                    AddDistinct(contour, points[1]);
                    break;
                case SKPathVerb.Quad:
                    contour ??= [points[0]];
                    AppendQuadratic(contour, points[0], points[1], points[2]);
                    break;
                case SKPathVerb.Conic:
                    contour ??= [points[0]];
                    AppendConic(contour, points[0], points[1], points[2], iterator.ConicWeight());
                    break;
                case SKPathVerb.Cubic:
                    contour ??= [points[0]];
                    AppendCubic(contour, points[0], points[1], points[2], points[3]);
                    break;
                case SKPathVerb.Close:
                    if (contour is { Count: > 1 })
                    {
                        AddDistinct(contour, contourStart);
                        yield return contour;
                    }
                    contour = null;
                    break;
                case SKPathVerb.Done:
                    if (contour is { Count: > 1 })
                        yield return contour;
                    yield break;
            }
        }
    }

    private void AppendQuadratic(List<SKPoint> output, SKPoint start, SKPoint control, SKPoint end)
    {
        var steps = CalculateCurveSteps(start, control, end);
        for (var i = 1; i <= steps; i++)
        {
            var t = i / (float)steps;
            var u = 1 - t;
            AddDistinct(output, new SKPoint(
                u * u * start.X + 2 * u * t * control.X + t * t * end.X,
                u * u * start.Y + 2 * u * t * control.Y + t * t * end.Y));
        }
    }

    private void AppendConic(List<SKPoint> output, SKPoint start, SKPoint control, SKPoint end, float weight)
    {
        var steps = CalculateCurveSteps(start, control, end);
        for (var i = 1; i <= steps; i++)
        {
            var t = i / (float)steps;
            var u = 1 - t;
            var startWeight = u * u;
            var controlWeight = 2 * weight * u * t;
            var endWeight = t * t;
            var denominator = startWeight + controlWeight + endWeight;
            if (Math.Abs(denominator) <= float.Epsilon)
                continue;

            AddDistinct(output, new SKPoint(
                (startWeight * start.X + controlWeight * control.X + endWeight * end.X) / denominator,
                (startWeight * start.Y + controlWeight * control.Y + endWeight * end.Y) / denominator));
        }
    }

    private void AppendCubic(List<SKPoint> output, SKPoint start, SKPoint control1, SKPoint control2, SKPoint end)
    {
        var steps = CalculateCurveSteps(start, control1, control2, end);
        for (var i = 1; i <= steps; i++)
        {
            var t = i / (float)steps;
            var u = 1 - t;
            AddDistinct(output, new SKPoint(
                u * u * u * start.X + 3 * u * u * t * control1.X + 3 * u * t * t * control2.X + t * t * t * end.X,
                u * u * u * start.Y + 3 * u * u * t * control1.Y + 3 * u * t * t * control2.Y + t * t * t * end.Y));
        }
    }

    private int CalculateCurveSteps(params SKPoint[] points)
    {
        var estimatedLength = 0f;
        for (var i = 1; i < points.Length; i++)
            estimatedLength += SKPoint.Distance(points[i - 1], points[i]);
        return Math.Clamp((int)Math.Ceiling(estimatedLength / Math.Max(0.001f, Tolerance.CurrentValue)), 2, 128);
    }

    private System.Numerics.Vector2 CalculateRelativePoint(SKPoint point)
    {
        var bounds = SourceBounds;
        var x = point.X - bounds.Left - bounds.Width * OffsetX.CurrentValue;
        var y = -(point.Y - bounds.Top - bounds.Height * OffsetY.CurrentValue);
        var radians = Rotation.CurrentValue * MathF.PI / 180f;
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        return new System.Numerics.Vector2(
            (x * cos - y * sin) * Scale,
            (x * sin + y * cos) * Scale);
    }

    private Color? ResolveSegmentColor(ShimPaint? paint)
    {
        var source = paint?.Color is { } color
            ? Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue)
            : Colors.Green;
        var output = ShowOriginColor
            ? source
            : IsForceColorful ? ColorfulLaneColor.Color : PickSimilarLaneColor(source)?.Color;
        return output is { } result
            ? Color.FromArgb((byte)Math.Clamp((int)Math.Round(result.A * Opacity.CurrentValue), 0, byte.MaxValue), result.R, result.G, result.B)
            : null;
    }

    private static SKTextAlign ConvertTextAlign(ShimSkiaSharp.SKTextAlign? align) => align switch
    {
        ShimSkiaSharp.SKTextAlign.Center => SKTextAlign.Center,
        ShimSkiaSharp.SKTextAlign.Right => SKTextAlign.Right,
        _ => SKTextAlign.Left
    };

    private static SKMatrix ToSkiaMatrix(ShimMatrix matrix) => new(
        matrix.ScaleX,
        matrix.SkewX,
        matrix.TransX,
        matrix.SkewY,
        matrix.ScaleY,
        matrix.TransY,
        matrix.Persp0,
        matrix.Persp1,
        matrix.Persp2);

    private static void AddDistinct(List<SKPoint> points, SKPoint point)
    {
        if (points.Count == 0 || SKPoint.Distance(points[^1], point) > 0.0001f)
            points.Add(point);
    }

    private void SetRangeValue(ref RangeValue field, RangeValue value, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (ReferenceEquals(field, value))
            return;

        field.PropertyChanged -= OnRangeValuePropertyChanged;
        field = value;
        field.PropertyChanged += OnRangeValuePropertyChanged;
        OnPropertyChanged(propertyName);
    }

    private void OnRangeValuePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(e.PropertyName ?? nameof(RangeValue.CurrentValue));
    }

    private void AttachRangeValues()
    {
        colorfulLaneBrightness.PropertyChanged += OnRangeValuePropertyChanged;
        rotation.PropertyChanged += OnRangeValuePropertyChanged;
        offsetX.PropertyChanged += OnRangeValuePropertyChanged;
        colorSimilar.PropertyChanged += OnRangeValuePropertyChanged;
        offsetY.PropertyChanged += OnRangeValuePropertyChanged;
        opacity.PropertyChanged += OnRangeValuePropertyChanged;
        tolerance.PropertyChanged += OnRangeValuePropertyChanged;
    }

    private void DetachRangeValues()
    {
        colorfulLaneBrightness.PropertyChanged -= OnRangeValuePropertyChanged;
        rotation.PropertyChanged -= OnRangeValuePropertyChanged;
        offsetX.PropertyChanged -= OnRangeValuePropertyChanged;
        colorSimilar.PropertyChanged -= OnRangeValuePropertyChanged;
        offsetY.PropertyChanged -= OnRangeValuePropertyChanged;
        opacity.PropertyChanged -= OnRangeValuePropertyChanged;
        tolerance.PropertyChanged -= OnRangeValuePropertyChanged;
    }

    private static RangeValue Clone(RangeValue value) => new()
    {
        MinValue = value.MinValue,
        MaxValue = value.MaxValue,
        IsLimitInt = value.IsLimitInt,
        CurrentValue = value.CurrentValue
    };

    private static double ColorDistance(Color left, Color right)
    {
        var redMean = (left.R + right.R) / 2.0;
        var red = left.R - right.R;
        var green = left.G - right.G;
        var blue = left.B - right.B;
        return Math.Sqrt(
            (2 + redMean / 256.0) * red * red +
            4 * green * green +
            (2 + (255 - redMean) / 256.0) * blue * blue);
    }
}
