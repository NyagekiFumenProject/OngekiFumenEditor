using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.CommandParserImpl
{
    [Export(typeof(ICommandParser))]
    public class ClickSECommandParser : ICommandParser
    {
        public string CommandLineHeader => ClickSE.CommandName;

        public OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var dataArr = args.GetDataArray<float>();
            var se = new ClickSE();

            se.TGrid.Unit = dataArr[1];
            se.TGrid.Grid = (int)dataArr[2];

            return se;
        }
    }
}
