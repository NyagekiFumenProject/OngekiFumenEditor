using OngekiFumenEditor.Kernel.Audio.Rhythm;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing;
using SkiaSharp;
using System.IO;
using System.Numerics;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.RhythmAnalysisCheck;

/// <summary>
/// 把节奏曲线画成对照图：白色镜像曲线即面板上那条曲线（几何全部来自生产代码 <see cref="RhythmGeometry"/>），
/// 可另叠一组「参考 BPM」虚线，用眼睛核对曲线峰值是否落在拍点上。
/// 这张图同时是几何构造的验证：列降采样、镜像、时间投影都走生产路径。
/// </summary>
internal static class SpectrumImageWriter
{
    private const int ImageWidth = 1600;
    private const int ImageHeight = 480;

    private static readonly Vector4 CurveColor = new(0.35f, 0.90f, 0.63f, 0.95f);
    private static readonly Vector4 ReferenceColor = new(0.25f, 0.85f, 1f, 0.85f);

    public static void Write(string path, RhythmEnvelope envelope, TimeSpan fromTime, TimeSpan toTime,
        double? referenceBpm, string title)
    {
        using var surface = SKSurface.Create(new SKImageInfo(ImageWidth, ImageHeight));
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(16, 16, 16));

        var points = new List<LineVertex>();

        RhythmGeometry.BuildCurve(points, envelope, fromTime, toTime, ImageWidth, ImageHeight, 0.9f, CurveColor, RhythmCurveTone.Default);
        Draw(canvas, points, 1.4f);

        if (referenceBpm is { } bpm and > 0)
        {
            points.Clear();
            var interval = TimeSpan.FromSeconds(60.0 / bpm);
            var first = TimeSpan.Zero;
            while (first < fromTime)
                first += interval;

            for (var time = first; time <= toTime; time += interval)
            {
                var x = WaveformGeometry.ProjectX(time, fromTime, (toTime - fromTime).TotalMilliseconds, ImageWidth);
                points.Add(new(new(x, -ImageHeight / 2), ReferenceColor, new VertexDash(2, 4)));
                points.Add(new(new(x, ImageHeight / 2), ReferenceColor, new VertexDash(2, 4)));
            }

            Draw(canvas, points, 1.2f);
        }

        using var font = new SKFont(SKTypeface.Default, 16);
        using var paint = new SKPaint { Color = new SKColor(230, 230, 230) };
        canvas.DrawText($"{title} | 曲线帧数 {envelope.FrameCount} | {fromTime.TotalSeconds:F1}s – {toTime.TotalSeconds:F1}s"
            + (referenceBpm is { } expect ? $" | 参考 {expect:F2} BPM" : ""), 12, 24, font, paint);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
        Console.WriteLine($"image: {path}");
    }

    /// <summary>把顶点对（两两成段）栅格化到 Skia 画布，支持简单虚线。</summary>
    private static void Draw(SKCanvas canvas, List<LineVertex> points, float strokeWidth)
    {
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth,
            IsAntialias = true,
        };

        for (var i = 0; i + 1 < points.Count; i += 2)
        {
            var a = points[i];
            var b = points[i + 1];
            var ax = a.Point.X + ImageWidth / 2f;
            var ay = ImageHeight / 2f - a.Point.Y;
            var bx = b.Point.X + ImageWidth / 2f;
            var by = ImageHeight / 2f - b.Point.Y;

            paint.Color = ToColor(a.Color);

            var dashSize = a.Dash.DashSize;
            var gapSize = a.Dash.GapSize;
            var length = MathF.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            if (gapSize <= 0 || length <= dashSize)
            {
                canvas.DrawLine(ax, ay, bx, by, paint);
                continue;
            }

            var dx = (bx - ax) / length;
            var dy = (by - ay) / length;
            for (var offset = 0f; offset < length; offset += dashSize + gapSize)
            {
                var end = MathF.Min(offset + dashSize, length);
                canvas.DrawLine(ax + dx * offset, ay + dy * offset, ax + dx * end, ay + dy * end, paint);
            }
        }
    }

    private static SKColor ToColor(Vector4 color)
        => new((byte)Math.Clamp(color.X * 255, 0, 255), (byte)Math.Clamp(color.Y * 255, 0, 255),
            (byte)Math.Clamp(color.Z * 255, 0, 255), (byte)Math.Clamp(color.W * 255, 0, 255));
}
