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
    public class ClickSECommandParser : INyagekiCommandParser
    {
        public string CommandName => "ClickSE";

        public void ParseAndApply(OngekiFumen fumen, string[] seg)
        {
            var clk = new ClickSE();

            clk.TGrid = seg[1].ParseToTGrid();

            fumen.AddObject(clk);
        }
    }
}
