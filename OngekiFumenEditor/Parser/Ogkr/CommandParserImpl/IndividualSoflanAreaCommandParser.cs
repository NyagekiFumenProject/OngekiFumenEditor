using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.Ogkr.CommandParserImpl
{
    [Export(typeof(ICommandParser))]
    public class IndividualSoflanAreaCommandParser : CommandParserBase
    {
        public override string CommandLineHeader => "ISF";

        public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var dataArr = args.GetDataArray<float>();
            var isf = new IndividualSoflanArea();

            var areaWidth = (int)dataArr[5];

            isf.TGrid.Unit = dataArr[1];
            isf.TGrid.Grid = (int)dataArr[2];
            isf.XGrid.Unit = dataArr[3] - areaWidth/2;
            isf.EndIndicator.TGrid = isf.TGrid + new GridOffset(0, (int)dataArr[4]);
            isf.EndIndicator.XGrid.Unit = dataArr[3] + areaWidth / 2;
            isf.SoflanGroup = (int)dataArr[6];

            return isf;
        }
    }
}
