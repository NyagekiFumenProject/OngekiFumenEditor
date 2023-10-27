using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.Svg;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;

namespace OngekiFumenEditor.Parser.DefaultImpl.Nyageki.CommandImpl.Objects
{
	[Export(typeof(INyagekiCommandParser))]
	public class SvgPrefabCommandParser : INyagekiCommandParser
	{
		public string CommandName => "SvgPrefab";

		public void ParseAndApply(OngekiFumen fumen, string[] seg)
		{
			var data = seg[1].Split(":", 2);

			using var d = data[0].GetValuesMapWithDisposable(out var map);

			var type = map["Type"];
			SvgPrefabBase svg = type switch
			{
				SvgImageFilePrefab.CommandName => new SvgImageFilePrefab(),
				SvgStringPrefab.CommandName => new SvgStringPrefab(),
				_ => default
			};

			if (svg is SvgImageFilePrefab imageFilePrefab)
			{
				var filePath = Encoding.UTF8.GetString(Convert.FromBase64String(map["FilePathBase64"]));
				if (File.Exists(filePath))
					imageFilePrefab.SvgFile = new FileInfo(filePath);
			}
			if (svg is SvgStringPrefab stringPrefab)
			{
				stringPrefab.Content = Encoding.UTF8.GetString(Convert.FromBase64String(map["Content"]));
				stringPrefab.TypefaceName = map["TypefaceName"];
				var colorId = int.Parse(map["FontColorId"]);
				stringPrefab.ColorfulLaneColor = ColorIdConst.AllColors.FirstOrDefault(x => x.Id == colorId);
				stringPrefab.FontSize = double.Parse(map["FontSize"]);
				stringPrefab.ContentFlowDirection = Enum.Parse<SvgStringPrefab.FlowDirection>(map["ContentFlowDirection"]);
				stringPrefab.ContentLineHeight = double.Parse(map["ContentLineHeight"]);
			}

			svg.OffsetX.CurrentValue = float.Parse(map["OffsetX"]);
			svg.OffsetY.CurrentValue = float.Parse(map["OffsetY"]);
			svg.ColorfulLaneBrightness.CurrentValue = float.Parse(map["Brightness"]);
			svg.ShowOriginColor = bool.Parse(map["ShowOriginColor"]);
			svg.ColorSimilar.CurrentValue = float.Parse(map["ColorSimilar"]);
			svg.Rotation.CurrentValue = float.Parse(map["Rotation"]);
			svg.EnableColorfulLaneSimilar = bool.Parse(map["EnableColorfulLaneSimilar"]);
			svg.Opacity.CurrentValue = float.Parse(map["Opacity"]);
			svg.Scale = float.Parse(map["Scale"]);
			svg.Tolerance.CurrentValue = float.Parse(map["Tolerance"]);
			svg.TGrid = map["T"].ParseToTGrid();
			svg.XGrid = map["X"].ParseToXGrid();

			fumen.AddObject(svg);
		}
	}
}
