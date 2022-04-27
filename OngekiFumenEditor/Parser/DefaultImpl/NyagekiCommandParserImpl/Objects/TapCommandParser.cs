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
    public class TapCommandParser : INyagekiCommandParser
    {
        public string CommandName => "Tap";

        public void ParseAndApply(OngekiFumen fumen, string[] seg)
        {
            //$"Tap:{tap.ReferenceLaneStrId}:X[{tap.XGrid.Unit},{tap.XGrid.Grid}],T[{tap.TGrid.Unit},{tap.TGrid.Grid}],C[{tap.IsCritical}]"
            var tap = new Tap();
            var data = seg[1].Split(":");

            var refRecordId = int.Parse(data[0]);
            tap.ReferenceLaneStart = fumen.Lanes.FirstOrDefault(x => x.RecordId == refRecordId);

            using var d = data[1].GetValuesMapWithDisposable(out var map);

            tap.TGrid = map["T"].ParseToTGrid();
            tap.XGrid = map["X"].ParseToXGrid();
            tap.IsCritical = bool.Parse(map["C"]);

            fumen.AddObject(tap);
        }
    }
}
