using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.OngekiObjects;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Core.Parser.DefaultImpl.Nyageki.CommandImpl.Objects
{
	[Export(typeof(INyagekiCommandParser))]
	public class ClickSECommandParser : INyagekiCommandParser
	{
		public string CommandName => "ClickSE";

		public void ParseAndApply(OngekiFumen fumen, string[] seg)
		{
			var clk = new ClickSE();

			clk.TGrid = seg[1].ParseToTGrid();

			fumen.AddObject(clk);
		}
	}
}
