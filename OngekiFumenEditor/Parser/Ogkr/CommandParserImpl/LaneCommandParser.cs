using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Utils;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl
{
	public abstract class LaneCommandParserBase : CommandParserBase
	{
		public void CommonParse(ConnectableObjectBase connectObject, CommandArgs args, OngekiFumen fumen)
		{
			var dataArr = args.GetDataArray<float>();

			connectObject.TGrid = new TGrid(dataArr[2], (int)dataArr[3]);
			connectObject.XGrid = new XGrid(dataArr[4]);

			if (connectObject is IColorfulLane colorfulLane)
			{
				var colorId = (int)dataArr[5];
				colorfulLane.ColorId = ColorIdConst.AllColors.FirstOrDefault(x => x.Id == colorId);
				colorfulLane.Brightness = (int)dataArr[6];
			}
		}
	}

	public abstract class LaneStartCommandParser<T> : LaneCommandParserBase where T : LaneStartBase, new()
	{
		public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
		{
			var laneRecordId = args.GetData<int>(1);
			var laneObject = new T()
			{
				RecordId = laneRecordId
			};

			CommonParse(laneObject, args, fumen);
			return laneObject;
		}
	}

	public abstract class LaneChildObjectCommandParser<T> : LaneCommandParserBase where T : ConnectableChildObjectBase, new()
	{
		public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
		{
			var beamRecordId = args.GetData<int>(1);
			if (fumen.Lanes.FirstOrDefault(x => x.RecordId == beamRecordId) is not ConnectableStartObject beamStart)
			{
				Log.LogError($"Can't parse {CommandLineHeader} command because beam record id not found : {beamRecordId}");
				return default;
			}

			var laneObject = new T();
			CommonParse(laneObject, args, fumen);
			beamStart.AddChildObject(laneObject);
			return null;
		}
	}

	#region Implements

	[Export(typeof(ICommandParser))]
	public class ColorfulLaneStartCommandParser : LaneStartCommandParser<ColorfulLaneStart>
	{
		public override string CommandLineHeader => "CLS";
	}

	[Export(typeof(ICommandParser))]
	public class ColorfulLaneNextCommandParser : LaneChildObjectCommandParser<ColorfulLaneNext>
	{
		public override string CommandLineHeader => "CLN";
	}

	[Export(typeof(ICommandParser))]
	public class ColorfulLaneEndCommandParser : ColorfulLaneNextCommandParser
	{
		public override string CommandLineHeader => "CLE";
	}

	[Export(typeof(ICommandParser))]
	public class LaneLeftStartCommandParser : LaneStartCommandParser<LaneLeftStart>
	{
		public override string CommandLineHeader => "LLS";
	}

	[Export(typeof(ICommandParser))]
	public class LaneCenterStartCommandParser : LaneStartCommandParser<LaneCenterStart>
	{
		public override string CommandLineHeader => "LCS";
	}

	[Export(typeof(ICommandParser))]
	public class EnemyLaneStartCommandParser : LaneStartCommandParser<EnemyLaneStart>
	{
		public override string CommandLineHeader => "ENS";
	}

	[Export(typeof(ICommandParser))]
	public class LaneRightStartCommandParser : LaneStartCommandParser<LaneRightStart>
	{
		public override string CommandLineHeader => "LRS";
	}

	[Export(typeof(ICommandParser))]
	public class LaneLeftNextCommandParser : LaneChildObjectCommandParser<LaneLeftNext>
	{
		public override string CommandLineHeader => "LLN";
	}

	[Export(typeof(ICommandParser))]
	public class LaneCenterNextCommandParser : LaneChildObjectCommandParser<LaneCenterNext>
	{
		public override string CommandLineHeader => "LCN";
	}

	[Export(typeof(ICommandParser))]
	public class EnemyLaneNextCommandParser : LaneChildObjectCommandParser<EnemyLaneNext>
	{
		public override string CommandLineHeader => "ENN";
	}

	[Export(typeof(ICommandParser))]
	public class LaneRightNextCommandParser : LaneChildObjectCommandParser<LaneRightNext>
	{
		public override string CommandLineHeader => "LRN";
	}

	[Export(typeof(ICommandParser))]
	public class LaneLeftEndCommandParser : LaneLeftNextCommandParser
	{
		public override string CommandLineHeader => "LLE";
	}

	[Export(typeof(ICommandParser))]
	public class LaneCenterEndCommandParser : LaneCenterNextCommandParser
	{
		public override string CommandLineHeader => "LCE";
	}

	[Export(typeof(ICommandParser))]
	public class LaneRightEndCommandParser : LaneRightNextCommandParser
	{
		public override string CommandLineHeader => "LRE";
	}

	[Export(typeof(ICommandParser))]
	public class EnemyLaneEndCommandParser : EnemyLaneNextCommandParser
	{
		public override string CommandLineHeader => "ENE";
	}

	[Export(typeof(ICommandParser))]
	public class AutoplayFaderLaneNextCommandParser : LaneChildObjectCommandParser<AutoplayFaderLaneNext>
	{
		public override string CommandLineHeader => "[APFN]";
	}

	[Export(typeof(ICommandParser))]
	public class AutoplayFaderLaneEndCommandParser : AutoplayFaderLaneNextCommandParser
	{
		public override string CommandLineHeader => "[APFE]";
	}

	[Export(typeof(ICommandParser))]
	public class AutoPlayFaderLaneStartCommandParser : LaneStartCommandParser<AutoplayFaderLaneStart>
	{
		public override string CommandLineHeader => "[APFS]";
	}
	#endregion
}
