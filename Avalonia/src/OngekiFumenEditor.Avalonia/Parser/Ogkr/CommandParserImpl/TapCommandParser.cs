using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Utils;
using System.Linq;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl
{
	[RegisterSingleton<ICommandParser>]
	public class TapCommandParser : CommandParserBase
	{
		public override string CommandLineHeader => "TAP";

		public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
		{
			var dataArr = args.GetDataArray<float>();

			var laneId = args.GetData<int>(1);
			var refLaneStart = fumen.Lanes.FirstOrDefault(x => x.RecordId == laneId);
			if (refLaneStart is null)
			{
				Log.LogWarn($"Tap parse can't find lane/wallLane RecordId = {laneId}");
			}
			var tap = new Tap();

			tap.ReferenceLaneStart = refLaneStart;

			tap.IsCritical = args.GetData<string>(0) == "CTP" || args.GetData<string>(0) == "XTP";

			tap.TGrid.Unit = dataArr[2];
			tap.TGrid.Grid = (int)dataArr[3];
			tap.XGrid.Unit = dataArr[4];
			tap.XGrid.Grid = (int)dataArr[5];
			return tap;
		}
	}

	[RegisterSingleton<ICommandParser>]
	public class CriticalTapCommandParser1 : TapCommandParser
	{
		public override string CommandLineHeader => "CTP";
	}

	[RegisterSingleton<ICommandParser>]
	public class CriticalTapCommandParser2 : TapCommandParser
	{
		public override string CommandLineHeader => "XTP";
	}
}


