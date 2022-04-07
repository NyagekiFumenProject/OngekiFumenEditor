using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OngekiFumenEditor.Base.EditorObjects.Svg
{
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
            new LaneColor(LaneType.Left,Colors.Red),
            new LaneColor(LaneType.Center,Colors.Green),
            new LaneColor(LaneType.Right,Colors.Blue),
            new LaneColor(LaneType.WallLeft,Color.FromRgb(181, 156, 231)),
            new LaneColor(LaneType.WallRight,Color.FromRgb(231, 149, 178))
        }.Concat(ColorIdConst.AllColors.Select(x => new LaneColor()
        {
            LaneType = LaneType.Colorful,
            Color = Color.FromArgb(x.Color.A, x.Color.R, x.Color.G, x.Color.B)
        }))).ToArray();
    }
}
