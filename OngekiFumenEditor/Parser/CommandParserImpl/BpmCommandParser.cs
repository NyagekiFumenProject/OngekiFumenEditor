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
    public class BpmCommandParser : ICommandParser
    {
        public string CommandLineHeader => "BPM";

        public IOngekiObject Parse(string line, OngekiFumen fumen)
        {
            var dataArr = ParserUtils.GetDataArray<float>(line);
            var bpm = new BPM();

            bpm.TGrid.Unit = dataArr[0];
            bpm.TGrid.Grid = (int)dataArr[1];
            bpm.Value = dataArr[2];

            return bpm;
        }
    }
}
