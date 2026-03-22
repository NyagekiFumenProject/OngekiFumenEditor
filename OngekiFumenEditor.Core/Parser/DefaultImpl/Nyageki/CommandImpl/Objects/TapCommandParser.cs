using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Parser.DefaultImpl.Nyageki.CommandImpl.Objects
{
	[Export(typeof(INyagekiCommandParser))]
	public class TapCommandParser : INyagekiCommandParser
	{
		public string CommandName => "Tap";

		public void ParseAndApply(OngekiFumen fumen, string[] seg)
		{
			//$"Tap:{tap.ReferenceLaneStrId}:X[{tap.XGrid.Unit},{tap.XGrid.Grid}],T[{tap.TGrid.Unit},{tap.TGrid.Grid}],C[{tap.IsCritical}]"

			var data = seg[1].Split(":");

			var refRecordId = int.Parse(data[0]);
			var refLane = fumen.Lanes.FirstOrDefault(x => x.RecordId == refRecordId);

			//if (refLane is null)
			//{
			//    throw new Exception($"Can't parse line as Tap/WallTap because reference lane ({refRecordId}) is not found.");
			//}

			using var d = data[1].GetValuesMapWithDisposable(out var map);
			//var isWall = (refLane?.IsWallLane ?? false) || (map.TryGetValue("W", out var w) ? bool.Parse(w) : false);
			var tap = new Tap();
			tap.ReferenceLaneStart = refLane;
			tap.TGrid = map["T"].ParseToTGrid();
			tap.XGrid = map["X"].ParseToXGrid();
			tap.IsCritical = bool.Parse(map["C"]);

			fumen.AddObject(tap);
		}
	}
}
