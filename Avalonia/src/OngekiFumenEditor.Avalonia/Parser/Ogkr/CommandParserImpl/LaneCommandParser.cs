using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.EditorObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Utils;
using System.Linq;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl
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
				RecordId = laneRecordId,
				IsTransparent = args.GetData<int>(7) > 0,
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

	[RegisterSingleton<ICommandParser>]
	public class ColorfulLaneStartCommandParser : LaneStartCommandParser<ColorfulLaneStart>
	{
		public override string CommandLineHeader => "CLS";
	}

	[RegisterSingleton<ICommandParser>]
	public class ColorfulLaneNextCommandParser : LaneChildObjectCommandParser<ColorfulLaneNext>
	{
		public override string CommandLineHeader => "CLN";
	}

	[RegisterSingleton<ICommandParser>]
	public class ColorfulLaneEndCommandParser : ColorfulLaneNextCommandParser
	{
		public override string CommandLineHeader => "CLE";
	}

	[RegisterSingleton<ICommandParser>]
	public class LaneLeftStartCommandParser : LaneStartCommandParser<LaneLeftStart>
	{
		public override string CommandLineHeader => "LLS";
	}

	[RegisterSingleton<ICommandParser>]
	public class LaneCenterStartCommandParser : LaneStartCommandParser<LaneCenterStart>
	{
		public override string CommandLineHeader => "LCS";
	}

	[RegisterSingleton<ICommandParser>]
	public class EnemyLaneStartCommandParser : LaneStartCommandParser<EnemyLaneStart>
	{
		public override string CommandLineHeader => "ENS";
	}

	[RegisterSingleton<ICommandParser>]
	public class LaneRightStartCommandParser : LaneStartCommandParser<LaneRightStart>
	{
		public override string CommandLineHeader => "LRS";
	}

	[RegisterSingleton<ICommandParser>]
	public class LaneLeftNextCommandParser : LaneChildObjectCommandParser<LaneLeftNext>
	{
		public override string CommandLineHeader => "LLN";
	}

	[RegisterSingleton<ICommandParser>]
	public class LaneCenterNextCommandParser : LaneChildObjectCommandParser<LaneCenterNext>
	{
		public override string CommandLineHeader => "LCN";
	}

	[RegisterSingleton<ICommandParser>]
	public class EnemyLaneNextCommandParser : LaneChildObjectCommandParser<EnemyLaneNext>
	{
		public override string CommandLineHeader => "ENN";
	}

	[RegisterSingleton<ICommandParser>]
	public class LaneRightNextCommandParser : LaneChildObjectCommandParser<LaneRightNext>
	{
		public override string CommandLineHeader => "LRN";
	}

	[RegisterSingleton<ICommandParser>]
	public class LaneLeftEndCommandParser : LaneLeftNextCommandParser
	{
		public override string CommandLineHeader => "LLE";
	}

	[RegisterSingleton<ICommandParser>]
	public class LaneCenterEndCommandParser : LaneCenterNextCommandParser
	{
		public override string CommandLineHeader => "LCE";
	}

	[RegisterSingleton<ICommandParser>]
	public class LaneRightEndCommandParser : LaneRightNextCommandParser
	{
		public override string CommandLineHeader => "LRE";
	}

	[RegisterSingleton<ICommandParser>]
	public class EnemyLaneEndCommandParser : EnemyLaneNextCommandParser
	{
		public override string CommandLineHeader => "ENE";
	}

	[RegisterSingleton<ICommandParser>]
	public class AutoplayFaderLaneNextCommandParser : LaneChildObjectCommandParser<AutoplayFaderLaneNext>
	{
		public override string CommandLineHeader => "[APFN]";
	}

	[RegisterSingleton<ICommandParser>]
	public class AutoplayFaderLaneEndCommandParser : AutoplayFaderLaneNextCommandParser
	{
		public override string CommandLineHeader => "[APFE]";
	}

	[RegisterSingleton<ICommandParser>]
	public class AutoPlayFaderLaneStartCommandParser : LaneStartCommandParser<AutoplayFaderLaneStart>
	{
		public override string CommandLineHeader => "[APFS]";
	}
	#endregion
}


