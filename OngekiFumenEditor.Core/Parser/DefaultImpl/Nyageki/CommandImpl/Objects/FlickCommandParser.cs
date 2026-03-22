using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Parser.DefaultImpl.Nyageki.CommandImpl.Objects
{
	[Export(typeof(INyagekiCommandParser))]
	public class FlickCommandParser : INyagekiCommandParser
	{
		public string CommandName => "Flick";

		public void ParseAndApply(OngekiFumen fumen, string[] seg)
		{
			//$"Flick:X[{flick.XGrid.Unit},{flick.XGrid.Grid}],T[{flick.TGrid.Unit},{flick.TGrid.Grid}],C[{flick.IsCritical}],D[{flick.Direction}]"
			var flick = new Flick();

			using var d = seg[1].GetValuesMapWithDisposable(out var map);

			flick.TGrid = map["T"].ParseToTGrid();
			flick.XGrid = map["X"].ParseToXGrid();
			flick.IsCritical = bool.Parse(map["C"]);
			flick.Direction = Enum.Parse<Flick.FlickDirection>(map["D"]);

			fumen.AddObject(flick);
		}
	}
}
