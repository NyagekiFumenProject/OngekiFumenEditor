using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;

namespace OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki.CommandImpl.Objects
{
	[RegisterSingleton<INyagekiCommandParser>]
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


