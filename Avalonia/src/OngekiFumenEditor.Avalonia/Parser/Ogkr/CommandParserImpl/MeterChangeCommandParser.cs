using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl
{
	[RegisterSingleton<ICommandParser>]
	public class MeterChangeCommandParser : CommandParserBase
	{
		public override string CommandLineHeader => MeterChange.CommandName;

		public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
		{
			var dataArr = args.GetDataArray<float>();
			var met = new MeterChange();

			met.TGrid.Unit = dataArr[1];
			met.TGrid.Grid = (int)dataArr[2];
			met.BunShi = (int)dataArr[3];
			met.Bunbo = (int)dataArr[4];

			return met;
		}
	}
}


