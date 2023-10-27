using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl
{
	[Export(typeof(ICommandParser))]
	public class ClickSECommandParser : CommandParserBase
	{
		public override string CommandLineHeader => ClickSE.CommandName;

		public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
		{
			var dataArr = args.GetDataArray<float>();
			var se = new ClickSE();

			se.TGrid.Unit = dataArr[1];
			se.TGrid.Grid = (int)dataArr[2];

			return se;
		}
	}
}
