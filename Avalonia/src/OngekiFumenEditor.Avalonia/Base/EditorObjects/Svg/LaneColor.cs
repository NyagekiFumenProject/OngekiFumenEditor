#nullable enable

using Avalonia.Media;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;

namespace OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;

public readonly record struct LaneColor(LaneType LaneType, Color Color)
{
    public static IReadOnlyList<LaneColor> AllLaneColors { get; } =
    [
        new(LaneType.WallLeft, Color.FromRgb(181, 156, 231)),
        new(LaneType.WallRight, Color.FromRgb(231, 149, 178)),
        .. ColorIdConst.SvgPrefabColors.Select(x => new LaneColor(
            x.Name switch
            {
                nameof(ColorIdConst.LaneBlue) => LaneType.Right,
                nameof(ColorIdConst.LaneRed) => LaneType.Left,
                nameof(ColorIdConst.LaneGreen) => LaneType.Center,
                _ => LaneType.Colorful
            },
            x.Color))
    ];
}
