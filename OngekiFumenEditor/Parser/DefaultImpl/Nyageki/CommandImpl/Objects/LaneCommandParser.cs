using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Parser.DefaultImpl.Nyageki.CommandImpl.Objects
{
	[Export(typeof(INyagekiCommandParser))]
	public class LaneCommandParser : INyagekiCommandParser
	{
		public string CommandName => "Lane";

		public void ParseAndApply(OngekiFumen fumen, string[] seg)
		{
			//Lane:0:(LCS,X[-12,0],T[69,1350]) -> (LCE,X[1.0094876,0],T[70,216])
			var data = seg[1].Split(":");

			var recordId = int.Parse(data[0]);
			var maps = data[1].Split("->").Select(x => x.Trim().TrimStart('(').TrimEnd(')')).Select(x => (x.GetValuesMapWithDisposable(out var d), d)).ToArray();
			var notes = maps.Select(x => x.d).ToArray();

			void buildCommon(ConnectableObjectBase obj, Dictionary<string, string> map)
			{
				obj.TGrid = map["T"].ParseToTGrid();
				obj.XGrid = map["X"].ParseToXGrid();
			}

			void buildColorfulLane(IColorfulLane obj, Dictionary<string, string> map)
			{
				var colorfulData = map["C"].Split(",");
				var colorId = colorfulData[0];
				var brightness = int.Parse(colorfulData[1]);
				obj.ColorId = ColorIdConst.AllColors.FirstOrDefault(x => colorId == x.Name);
				obj.Brightness = brightness;
			}

			// Start
			var startData = notes.FirstOrDefault();
			ConnectableStartObject startObject = startData["Type"] switch
			{
				"LCS" => new LaneCenterStart(),
				"LRS" => new LaneRightStart(),
				"LLS" => new LaneLeftStart(),
				"CLS" => new ColorfulLaneStart(),
				"WLS" => new WallLeftStart(),
				"WRS" => new WallRightStart(),
				"ENS" => new EnemyLaneStart(),
				"[APFS]" => new AutoplayFaderLaneStart(),
				_ => null
			};
			buildCommon(startObject, startData);
			if (startObject is IColorfulLane colorfulLane)
				buildColorfulLane(colorfulLane, startData);
			startObject.RecordId = recordId;

			// Next/End
			foreach (var childData in notes.Skip(1))
			{
				var childObject = startObject.CreateChildObject();

				buildCommon(childObject, childData);
				if (childObject is IColorfulLane colorfulLane2)
					buildColorfulLane(colorfulLane2, childData);
				startObject.AddChildObject(childObject);
			}

			foreach ((var d, _) in maps)
				d.Dispose();

			fumen.AddObject(startObject);
		}
	}
}
