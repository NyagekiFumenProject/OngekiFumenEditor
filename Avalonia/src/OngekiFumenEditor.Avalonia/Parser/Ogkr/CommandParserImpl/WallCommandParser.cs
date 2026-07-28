using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Utils;
using System.Linq;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl
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

	[RegisterSingleton<ICommandParser>]
	public class WallLeftStartCommandParser : WallStartCommandParser<WallLeftStart>
	{
		public override string CommandLineHeader => "WLS";
	}

	[RegisterSingleton<ICommandParser>]
	public class WallLeftNextCommandParser : WallNextCommandParser<WallLeftNext>
	{
		public override string CommandLineHeader => "WLN";
	}

	[RegisterSingleton<ICommandParser>]
	public class WallLeftEndommandParser : WallNextCommandParser<WallLeftNext>
	{
		public override string CommandLineHeader => "WLE";
	}

	[RegisterSingleton<ICommandParser>]
	public class WallRightStartCommandParser : WallStartCommandParser<WallRightStart>
	{
		public override string CommandLineHeader => "WRS";
	}

	[RegisterSingleton<ICommandParser>]
	public class WallRightNextCommandParser : WallNextCommandParser<WallRightNext>
	{
		public override string CommandLineHeader => "WRN";
	}

	[RegisterSingleton<ICommandParser>]
	public class WallRightEndommandParser : WallNextCommandParser<WallRightNext>
	{
		public override string CommandLineHeader => "WRE";
	}

	#endregion
}


