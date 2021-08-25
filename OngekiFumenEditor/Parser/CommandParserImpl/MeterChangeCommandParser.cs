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
    public class MeterChangeCommandParser : ICommandParser
    {
        public string CommandLineHeader => MeterChange.CommandName;

        public OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var dataArr = args.GetDataArray<float>();
            var met = new MeterChange();

            met.TGrid.Unit = dataArr[1];
            met.TGrid.Grid = (int)dataArr[2];
            met.BunShi = (int)dataArr[3];
            met.Bunbo = (int)dataArr[4];

            return met;
        }
    }
}
