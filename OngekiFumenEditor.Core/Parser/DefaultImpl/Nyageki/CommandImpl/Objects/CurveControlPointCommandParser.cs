using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Parser.DefaultImpl.Nyageki.CommandImpl.Objects
{
	[Export(typeof(INyagekiCommandParser))]
	public class CurveControlPointCommandParser : INyagekiCommandParser
	{
		public string CommandName => "CurveControlPoint";

		public void ParseAndApply(OngekiFumen fumen, string[] seg)
		{
			//$"Hold:{hold.ReferenceLaneStrId},{hold.IsCritical}:(X[{hold.XGrid.Unit},{hold.XGrid.Grid}],T[{hold.TGrid.Unit},{hold.TGrid.Grid}]) -> (X[{end.XGrid.Unit},{end.XGrid.Grid}],T[{end.TGrid.Unit},{end.TGrid.Grid}])"
			var data = seg[1].Split(":");
			var recordId = int.Parse(data[0]);
			var refIdName = data[1].Trim();
			var childIdx = int.Parse(data[2]);
			var curvePrecision = float.Parse(data[3]);
			var curPathData = data[4];

			var childObj = fumen.Lanes
				.AsEnumerable<ConnectableStartObject>()
				.Concat(fumen.Beams).FirstOrDefault(x => x.RecordId == recordId && x.IDShortName == refIdName)
				?.Children?.ElementAtOrDefault(childIdx);

			childObj.CurvePrecision = curvePrecision;

			var maps = curPathData.Split("...").Select(x => x.Trim().TrimStart('(').TrimEnd(')')).Select(x => (x.GetValuesMapWithDisposable(out var d), d)).ToArray();

			foreach (var curPointData in maps.Select(x => x.d))
			{
				var laneCurvePathControl = new LaneCurvePathControlObject();
				laneCurvePathControl.TGrid = curPointData["T"].ParseToTGrid();
				laneCurvePathControl.XGrid = curPointData["X"].ParseToXGrid();

				childObj.AddControlObject(laneCurvePathControl);
			}

			foreach ((var d, _) in maps)
				d.Dispose();
		}
	}
}
