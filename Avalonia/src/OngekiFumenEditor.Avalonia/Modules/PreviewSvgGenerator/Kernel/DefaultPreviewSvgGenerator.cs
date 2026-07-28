using System.Globalization;
using System.Text;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Utils.Ogkr;

namespace OngekiFumenEditor.Avalonia.Modules.PreviewSvgGenerator.Kernel;

[RegisterSingleton<IPreviewSvgGenerator>]
public class DefaultPreviewSvgGenerator : IPreviewSvgGenerator
{
    public async Task<byte[]> GenerateSvgAsync(OngekiFumen rawFumen, SvgGenerateOption option)
    {
        var fumen = await StandardizeFormat.CopyFumenObject(rawFumen);
        if (option.SoflanMode == SoflanMode.AbsSoflan)
        {
            foreach (var sfl in fumen.SoflansMap.Values.SelectMany(x => x))
                sfl.ApplySpeedInDesignMode = true;
        }

        var specifySoflans = fumen.SoflansMap.DefaultSoflanList;
        var maxTGrid = TGridCalculator.ConvertAudioTimeToTGrid(option.Duration, fumen.BpmList);

        var totalHeight = option.SoflanMode == SoflanMode.Soflan
            ? TGridCalculator.ConvertTGridToY_PreviewMode(maxTGrid, specifySoflans, fumen.BpmList, option.VerticalScale)
            : TGridCalculator.ConvertTGridToY_DesignMode(maxTGrid, fumen.SoflansMap.DefaultSoflanList, fumen.BpmList,
                option.VerticalScale);

        var ctx = new GenerateContext
        {
            Fumen = fumen,
            SpecifySoflans = specifySoflans,
            Option = option,
            TotalHeight = totalHeight,
            MaxTGrid = maxTGrid
        };

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine(
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{Fmt(ctx.TotalWidth)}\" height=\"{Fmt(ctx.TotalHeight)}\" viewBox=\"0 0 {Fmt(ctx.TotalWidth)} {Fmt(ctx.TotalHeight)}\">");
        sb.AppendLine("<rect x=\"0\" y=\"0\" width=\"100%\" height=\"100%\" fill=\"#101216\"/>");

        SerializeEvents(sb, ctx);
        SerializeLanes(sb, ctx);
        SerializeHolds(sb, ctx);
        SerializeBeams(sb, ctx);
        SerializeTapAndFlick(sb, ctx);
        SerializeBell(sb, ctx);

        sb.AppendLine("</svg>");

        var data = Encoding.UTF8.GetBytes(sb.ToString());
        if (!string.IsNullOrWhiteSpace(option.OutputFilePath))
            await File.WriteAllBytesAsync(option.OutputFilePath, data);

        return data;
    }

    private static void SerializeEvents(StringBuilder sb, GenerateContext ctx)
    {
        sb.AppendLine("<g id=\"events\" opacity=\"0.35\">");

        foreach (var bpm in ctx.Fumen.BpmList.Skip(1))
        {
            var y = ctx.CalculateToY(bpm.TGrid, ctx.Fumen.SoflansMap.DefaultSoflanList);
            sb.AppendLine(
                $"<line x1=\"0\" y1=\"{Fmt(y)}\" x2=\"{Fmt(ctx.TotalWidth)}\" y2=\"{Fmt(y)}\" stroke=\"#6ec1ff\" stroke-width=\"1\"/>");
        }

        foreach (var meter in ctx.Fumen.MeterChanges.Skip(1))
        {
            var y = ctx.CalculateToY(meter.TGrid, ctx.Fumen.SoflansMap.DefaultSoflanList);
            sb.AppendLine(
                $"<line x1=\"0\" y1=\"{Fmt(y)}\" x2=\"{Fmt(ctx.TotalWidth)}\" y2=\"{Fmt(y)}\" stroke=\"#a9ffb3\" stroke-width=\"1\"/>");
        }

        sb.AppendLine("</g>");
    }

    private static void SerializeLanes(StringBuilder sb, GenerateContext ctx)
    {
        sb.AppendLine("<g id=\"lanes\">");
        foreach (var lane in ctx.Fumen.Lanes)
        {
            var color = lane.LaneType switch
            {
                LaneType.Left => "#ff4d4f",
                LaneType.Center => "#34c759",
                LaneType.Right => "#3b82f6",
                LaneType.WallLeft => "#b59ce7",
                LaneType.WallRight => "#e795b2",
                LaneType.Colorful => "#f6d365",
                _ => "#cccccc"
            };

            var points = lane.GenAllPath()
                .Select(x => x.pos)
                .Select(p =>
                    $"{Fmt(ctx.CalculateToX(p.X * 1.0 / XGrid.DEFAULT_RES_X))},{Fmt(ctx.CalculateToY(p.Y * 1.0 / TGrid.DEFAULT_RES_T, ctx.Fumen.SoflansMap.DefaultSoflanList))}")
                .ToArray();

            if (points.Length < 2)
                continue;

            var width = lane.IsWallLane ? 4 : 2;
            sb.AppendLine(
                $"<polyline points=\"{string.Join(" ", points)}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{width}\" stroke-linecap=\"round\"/>");
        }

        sb.AppendLine("</g>");
    }

    private static void SerializeHolds(StringBuilder sb, GenerateContext ctx)
    {
        sb.AppendLine("<g id=\"holds\" opacity=\"0.9\">");
        foreach (var hold in ctx.Fumen.Holds)
        {
            if (hold.HoldEnd is not HoldEnd end)
                continue;

            var y1 = ctx.CalculateToY(hold.TGrid, ctx.Fumen.SoflansMap.DefaultSoflanList);
            var y2 = ctx.CalculateToY(end.TGrid, ctx.Fumen.SoflansMap.DefaultSoflanList);
            var x1 = ctx.CalculateToX(hold.XGrid);
            var x2 = ctx.CalculateToX(end.XGrid);

            var color = hold.ReferenceLaneStart?.LaneType switch
            {
                LaneType.Left => "#ff6b6b",
                LaneType.Center => "#7dff8a",
                LaneType.Right => "#6ea8ff",
                LaneType.WallLeft => "#c9b6ff",
                LaneType.WallRight => "#ffb1cf",
                _ => "#f0f0f0"
            };

            sb.AppendLine(
                $"<line x1=\"{Fmt(x1)}\" y1=\"{Fmt(y1)}\" x2=\"{Fmt(x2)}\" y2=\"{Fmt(y2)}\" stroke=\"{color}\" stroke-width=\"10\" stroke-linecap=\"round\"/>");
        }

        sb.AppendLine("</g>");
    }

    private static void SerializeBeams(StringBuilder sb, GenerateContext ctx)
    {
        sb.AppendLine("<g id=\"beams\" opacity=\"0.85\">");
        foreach (var beam in ctx.Fumen.Beams)
        {
            var points = beam.GenAllPath()
                .Select(x => x.pos)
                .Select(p =>
                    $"{Fmt(ctx.CalculateToX(p.X * 1.0 / XGrid.DEFAULT_RES_X))},{Fmt(ctx.CalculateToY(p.Y * 1.0 / TGrid.DEFAULT_RES_T, ctx.Fumen.SoflansMap.DefaultSoflanList))}")
                .ToArray();

            if (points.Length < 2)
                continue;

            var width = beam.WidthId.WidthDraw;
            sb.AppendLine(
                $"<polyline points=\"{string.Join(" ", points)}\" fill=\"none\" stroke=\"#ffd54a\" stroke-width=\"{Fmt(width)}\" stroke-linecap=\"round\"/>");
        }

        sb.AppendLine("</g>");
    }

    private static void SerializeTapAndFlick(StringBuilder sb, GenerateContext ctx)
    {
        sb.AppendLine("<g id=\"tapflick\">");

        foreach (var tap in ctx.Fumen.Taps)
        {
            var x = ctx.CalculateToX(tap.XGrid);
            var y = ctx.CalculateToY(tap.TGrid, ctx.Fumen.SoflansMap.DefaultSoflanList);
            var color = tap.BelongLaneType switch
            {
                LaneType.Left => "#ff4d4f",
                LaneType.Center => "#34c759",
                LaneType.Right => "#3b82f6",
                LaneType.WallLeft => "#b59ce7",
                LaneType.WallRight => "#e795b2",
                _ => "#ffffff"
            };
            var radius = tap.IsCritical ? 12 : 9;
            sb.AppendLine(
                $"<circle cx=\"{Fmt(x)}\" cy=\"{Fmt(y)}\" r=\"{radius}\" fill=\"{color}\" stroke=\"{(tap.IsCritical ? "#ffe066" : "#101216")}\" stroke-width=\"2\"/>");
        }

        foreach (var flick in ctx.Fumen.Flicks)
        {
            var x = ctx.CalculateToX(flick.XGrid);
            var y = ctx.CalculateToY(flick.TGrid, ctx.Fumen.SoflansMap.DefaultSoflanList);
            var dir = flick.Direction == Flick.FlickDirection.Right ? 1 : -1;
            var p1 = $"{Fmt(x - 14 * dir)},{Fmt(y - 10)}";
            var p2 = $"{Fmt(x + 14 * dir)},{Fmt(y)}";
            var p3 = $"{Fmt(x - 14 * dir)},{Fmt(y + 10)}";
            sb.AppendLine(
                $"<polygon points=\"{p1} {p2} {p3}\" fill=\"#f59e0b\" stroke=\"{(flick.IsCritical ? "#ffe066" : "#101216")}\" stroke-width=\"2\"/>");
        }

        sb.AppendLine("</g>");
    }

    private static void SerializeBell(StringBuilder sb, GenerateContext ctx)
    {
        sb.AppendLine("<g id=\"bell\">");
        foreach (var bell in ctx.Fumen.Bells)
        {
            var x = ctx.CalculateToX(bell.XGrid);
            var y = ctx.CalculateToY(bell.TGrid, ctx.Fumen.SoflansMap.DefaultSoflanList);
            sb.AppendLine($"<circle cx=\"{Fmt(x)}\" cy=\"{Fmt(y)}\" r=\"7\" fill=\"#fef08a\" stroke=\"#111\" stroke-width=\"1.5\"/>");
            sb.AppendLine(
                $"<line x1=\"{Fmt(x)}\" y1=\"{Fmt(y - 7)}\" x2=\"{Fmt(x)}\" y2=\"{Fmt(y - 13)}\" stroke=\"#fef08a\" stroke-width=\"2\"/>");
        }

        sb.AppendLine("</g>");
    }

    private static string Fmt(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}

