using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.OngekiObjects;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Core.Parser.Ogkr.CommandParserImpl
{
	[Export(typeof(ICommandParser))]
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
