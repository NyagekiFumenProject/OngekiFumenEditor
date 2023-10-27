using OngekiFumenEditor.Base.OngekiObjects;
using System.Collections.Generic;
using System.Linq;
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
			new LaneColor(LaneType.WallLeft,Color.FromRgb(181, 156, 231)),
			new LaneColor(LaneType.WallRight,Color.FromRgb(231, 149, 178))
		}.Concat(ColorIdConst.AllColors.Select(x => new LaneColor()
		{
			LaneType = x.Name switch
			{
				"LaneBlue" => LaneType.Right,
				"LaneRed" => LaneType.Left,
				"LaneGreen" => LaneType.Center,
				_ => LaneType.Colorful
			},
			Color = Color.FromArgb(x.Color.A, x.Color.R, x.Color.G, x.Color.B)
		}))).ToArray();
	}
}
