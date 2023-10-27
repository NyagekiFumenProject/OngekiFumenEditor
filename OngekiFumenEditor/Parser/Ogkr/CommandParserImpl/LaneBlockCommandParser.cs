using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Utils;
using System.ComponentModel.Composition;
using System.Linq;
using static OngekiFumenEditor.Base.OngekiObjects.LaneBlockArea;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl
{
	[Export(typeof(ICommandParser))]
	public class LaneBlockCommandParser : CommandParserBase
	{
		public override string CommandLineHeader => "LBK";

		public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
		{
			var dataArr = args.GetDataArray<float>();
			var lbk = new LaneBlockArea();
			var laneRecId = (int)dataArr[1];

			var refLaneType = fumen.Lanes.FirstOrDefault(x => x.RecordId == laneRecId)?.LaneType;
			lbk.Direction = refLaneType == LaneType.WallLeft ? BlockDirection.Left : BlockDirection.Right;

			lbk.TGrid = new TGrid(dataArr[2], (int)dataArr[3]);
			lbk.EndIndicator.TGrid = new TGrid(dataArr[6], (int)dataArr[7]);

			return lbk;
		}
	}
}
