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
    public class BellCommandParser : ICommandParser
    {
        public string CommandLineHeader => Bell.CommandName;

        public OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var dataArr = args.GetDataArray<float>();
            var bell = new Bell();

            bell.TGrid.Unit = dataArr[1];
            bell.TGrid.Grid = (int)dataArr[2];
            bell.XGrid.Unit = dataArr[3];

            return bell;
        }
    }
}
