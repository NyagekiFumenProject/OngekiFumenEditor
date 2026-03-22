using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Parser.DefaultImpl.Nyageki.CommandImpl.Objects
{
	[Export(typeof(INyagekiCommandParser))]
	public class LaneBlockCommandParser : INyagekiCommandParser
	{
		public string CommandName => "LaneBlock";

		public void ParseAndApply(OngekiFumen fumen, string[] seg)
		{
			//$"LaneBlock:{blk.Direction}:(T[{blk.TGrid.Unit},{blk.TGrid.Grid}]) -> (T[{blk.EndIndicator.TGrid.Unit},{blk.EndIndicator.TGrid.Grid}])"
			var blk = new LaneBlockArea();
			var data = seg[1].Split(":");

			blk.Direction = Enum.Parse<LaneBlockArea.BlockDirection>(data[0]);

			var maps = data[1].Split("->").Select(x => x.Trim().TrimStart('(').TrimEnd(')')).Select(x => (x.GetValuesMapWithDisposable(out var d), d)).ToArray();
			var notes = maps.Select(x => x.d).ToArray();

			blk.TGrid = notes[0]["T"].ParseToTGrid();
			blk.EndIndicator.TGrid = notes[1]["T"].ParseToTGrid();

			foreach ((var d, _) in maps)
				d.Dispose();

			fumen.AddObject(blk);
		}
	}
}
