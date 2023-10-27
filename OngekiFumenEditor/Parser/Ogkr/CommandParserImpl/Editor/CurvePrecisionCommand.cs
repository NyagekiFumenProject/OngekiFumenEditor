using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl.Editor
{
	[Export(typeof(ICommandParser))]
	public class CurvePrecisionCommand : CommandParserBase
	{
		public override string CommandLineHeader => "LCO_PREC";

		public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
		{
			var data = args.GetDataArray<float>();

			var starts = fumen.Lanes.AsEnumerable<ConnectableStartObject>().Concat(fumen.Beams);

			var laneId = (int)data[1];
			var name = args.GetData<string>(4);
			var childOrder = (int)data[2];
			if (starts.FirstOrDefault(x => x.RecordId == laneId && name == x.IDShortName) is not ConnectableStartObject start)
				throw new Exception($"can't parse LCO_CTRL because lane object (laneId:{laneId} name:{name}) is not found.");
			if (start.Children.ElementAt(childOrder) is not ConnectableChildObjectBase child)
				throw new Exception($"can't parse LCO_CTRL because child object (childOrder:{childOrder} name:{name} laneId:{laneId}) is not found.");

			child.CurvePrecision = data[3];
			return null;
		}
	}
}
