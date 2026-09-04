using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using System;
using System.Linq;

namespace OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki.CommandImpl.Objects
{
	[RegisterSingleton<INyagekiCommandParser>]
	public class LaneBlockCommandParser : INyagekiCommandParser
	{
		public string CommandName => "LaneBlock";

		public void ParseAndApply(OngekiFumen fumen, string[] seg)
		{
			//$"LaneBlock:{blk.Direction}:(T[{blk.TGrid.Unit},{blk.TGrid.Grid}]) -> (T[{blk.EndIndicator.TGrid.Unit},{blk.EndIndicator.TGrid.Grid}])"
			var blk = new LaneBlockArea();
			var data = seg[1].Split(":");

			blk.Direction = Enum.Parse<LaneBlockArea.BlockDirection>(data[0]);

			var notes = data[1].Split("->")
				.Select(x => x.Trim().TrimStart('(').TrimEnd(')').GetValuesMap())
				.ToArray();

			blk.TGrid = notes[0]["T"].ParseToTGrid();
			blk.EndIndicator.TGrid = notes[1]["T"].ParseToTGrid();


			fumen.AddObject(blk);
		}
	}
}


