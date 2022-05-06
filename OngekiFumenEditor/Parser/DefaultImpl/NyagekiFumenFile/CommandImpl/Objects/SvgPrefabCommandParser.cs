using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.Svg;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.DefaultImpl.NyagekiFumenFile.CommandImpl.Objects
{
    [Export(typeof(INyagekiCommandParser))]
    public class SvgPrefabCommandParser : INyagekiCommandParser
    {
        public string CommandName => "SvgPrefab";

        public void ParseAndApply(OngekiFumen fumen, string[] seg)
        {
            var svg = new SvgPrefab();

            var data = seg[1].Split(":", 2);

            svg.SvgFile = new System.IO.FileInfo(data[1].Trim());

            using var d = data[0].GetValuesMapWithDisposable(out var map);

            svg.OffsetX.CurrentValue = float.Parse(map["OffsetX"]);
            svg.OffsetY.CurrentValue = float.Parse(map["OffsetY"]);
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
