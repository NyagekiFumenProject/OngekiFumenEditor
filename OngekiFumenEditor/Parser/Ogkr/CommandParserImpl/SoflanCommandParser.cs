using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl
{
	[Export(typeof(ICommandParser))]
	public class SoflanCommandParser : CommandParserBase
	{
		public override string CommandLineHeader => "SFL";

		public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
		{
			var dataArr = args.GetDataArray<float>();
			var sfl = new Soflan();

			sfl.TGrid.Unit = dataArr[1];
			sfl.TGrid.Grid = (int)dataArr[2];

			var length = (int)dataArr[3];
			sfl.EndIndicator.TGrid = sfl.TGrid + new GridOffset(0, length);

			sfl.Speed = dataArr[4];

			return sfl;
		}
	}
}
