using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Utils;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl
{
	public abstract class WallCommandParserBase : CommandParserBase
	{
		public void CommonParse(ConnectableObjectBase beam, CommandArgs args, OngekiFumen fumen)
		{
			var dataArr = args.GetDataArray<float>();

			//todo add BeamTrack
			beam.TGrid = new TGrid(dataArr[2], (int)dataArr[3]);
			beam.XGrid = new XGrid(dataArr[4]);
		}
	}

	public abstract class WallStartCommandParser<T> : WallCommandParserBase where T : ConnectableStartObject, new()
	{
		public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
		{
			var beamRecordId = args.GetData<int>(1);
			var beam = new T()
			{
				RecordId = beamRecordId
			};
			//fumen.AddObject(beam);

			CommonParse(beam, args, fumen);

			return beam;
		}
	}

	public abstract class WallNextCommandParser<T> : WallCommandParserBase where T : ConnectableChildObjectBase, new()
	{
		public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
		{
			var beamRecordId = args.GetData<int>(1);
			if (fumen.Lanes.FirstOrDefault(x => x.RecordId == beamRecordId) is not ConnectableStartObject beamStart)
			{
				Log.LogError($"Can't parse {CommandLineHeader} command because beam record id not found : {beamRecordId}");
				return default;
			}

			var beam = new T();
			CommonParse(beam, args, fumen);
			beamStart.AddChildObject(beam);
			return beam;
		}
	}

	#region Implements

	[Export(typeof(ICommandParser))]
	public class WallLeftStartCommandParser : WallStartCommandParser<WallLeftStart>
	{
		public override string CommandLineHeader => "WLS";
	}

	[Export(typeof(ICommandParser))]
	public class WallLeftNextCommandParser : WallNextCommandParser<WallLeftNext>
	{
		public override string CommandLineHeader => "WLN";
	}

	[Export(typeof(ICommandParser))]
	public class WallLeftEndommandParser : WallNextCommandParser<WallLeftNext>
	{
		public override string CommandLineHeader => "WLE";
	}

	[Export(typeof(ICommandParser))]
	public class WallRightStartCommandParser : WallStartCommandParser<WallRightStart>
	{
		public override string CommandLineHeader => "WRS";
	}

	[Export(typeof(ICommandParser))]
	public class WallRightNextCommandParser : WallNextCommandParser<WallRightNext>
	{
		public override string CommandLineHeader => "WRN";
	}

	[Export(typeof(ICommandParser))]
	public class WallRightEndommandParser : WallNextCommandParser<WallRightNext>
	{
		public override string CommandLineHeader => "WRE";
	}

	#endregion
}
