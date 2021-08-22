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
        public string CommandLineHeader => "BEL";

        public IOngekiObject Parse(string line, OngekiFumen fumen)
        {
            var dataArr = ParserUtils.SplitData<float>(line);
            var bell = new Bell();

            bell.TGrid.Unit = dataArr[0];
            bell.TGrid.Grid = (int)dataArr[1];
            bell.XGrid.Unit = dataArr[2];

            return bell;
        }
    }
}
