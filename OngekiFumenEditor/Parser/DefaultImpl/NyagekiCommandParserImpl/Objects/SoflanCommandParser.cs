using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.DefaultImpl.NyagekiCommandParserImpl.Objects
{
    [Export(typeof(INyagekiCommandParser))]
    public class SoflanCommandParser : INyagekiCommandParser
    {
        public string CommandName => "Soflan";

        public void ParseAndApply(OngekiFumen fumen, string[] seg)
        {
            //$"Soflan:{soflan.Speed}:(T[{soflan.TGrid.Unit},{soflan.TGrid.Grid}]) -> (T[{soflan.EndTGrid.Unit},{soflan.EndTGrid.Grid}])"
            var soflan = new Soflan();
            var data = seg[1].Split(":");

            soflan.Speed = float.Parse(data[0]);
            var tgridRange = data[1].Split("->").Select(x=>x.Trim().TrimStart('(').TrimEnd(')')).Select(x=>x.ParseToTGrid()).ToArray();

            soflan.TGrid = tgridRange[0];
            soflan.EndIndicator.TGrid = tgridRange[1];

            fumen.AddObject(soflan);
        }
    }
}
