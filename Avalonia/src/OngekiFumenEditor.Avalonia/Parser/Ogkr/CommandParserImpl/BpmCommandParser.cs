using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl
{
	[RegisterSingleton<ICommandParser>]
	public class BpmCommandParser : CommandParserBase
	{
		public override string CommandLineHeader => BPMChange.CommandName;

		public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
		{
			var dataArr = args.GetDataArray<float>();
			var bpm = new BPMChange();

			bpm.TGrid.Unit = dataArr[1];
			bpm.TGrid.Grid = (int)dataArr[2];
			bpm.BPM = dataArr[3];

			return bpm;
		}
	}
}


