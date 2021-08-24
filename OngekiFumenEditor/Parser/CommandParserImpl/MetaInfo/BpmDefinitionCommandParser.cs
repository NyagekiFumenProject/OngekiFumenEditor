using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.CommandParserImpl.MetaInfo
{
    [Export(typeof(ICommandParser))]
    class BpmDefinitionCommandParser : MetaInfoCommandParserBase
    {
        public override string CommandLineHeader => "BPM_DEF";

        public override void ParseMetaInfo(string line, OngekiFumen fumen)
        {
            var dataArr = ParserUtils.GetDataArray<double>(line);

            fumen.MetaInfo.BpmDefinition = new FumenMetaInfo.BpmDef()
            {
                First = dataArr.ElementAtOrDefault(0),
                Common = dataArr.ElementAtOrDefault(1),
                Minimum = dataArr.ElementAtOrDefault(2),
                Maximum = dataArr.ElementAtOrDefault(3),
            };
            fumen.MetaInfo.FirstBpm = new BPM()
            {
                TGrid = new TGrid()
                {
                    Grid = 0,
                    Unit = 0
                },
                Value = fumen.MetaInfo.BpmDefinition.First,
            };
        }
    }
}
