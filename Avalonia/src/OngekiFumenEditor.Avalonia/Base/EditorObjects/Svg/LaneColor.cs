using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using System.Drawing;

namespace OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;

public struct LaneColor
{
    public LaneColor(LaneType laneType, Color color)
    {
        LaneType = laneType;
        Color = color;
    }

    public LaneType LaneType { get; set; }
    public Color Color { get; set; }

    public static IEnumerable<LaneColor> AllLaneColors { get; } = (new[]
    {
        new LaneColor(LaneType.WallLeft, Color.FromArgb(255, 181, 156, 231)),
        new LaneColor(LaneType.WallRight, Color.FromArgb(255, 231, 149, 178))
    }.Concat(ColorIdConst.AllColors.Select(x => new LaneColor
    {
        LaneType = x.Name switch
        {
            "LaneBlue" => LaneType.Right,
            "Aoi" => LaneType.Right,
            "LaneRed" => LaneType.Left,
            "Akane" => LaneType.Left,
            "LaneGreen" => LaneType.Center,
            "LaneG" => LaneType.Center,
            _ => LaneType.Colorful
        },
        Color = Color.FromArgb(x.Color.A, x.Color.R, x.Color.G, x.Color.B)
    }))).ToArray();
}

