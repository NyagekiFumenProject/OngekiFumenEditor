using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.CommandParserImpl.MetaInfo
{
    [Export(typeof(ICommandParser))]
    class MeterDefinitionCommandParser : MetaInfoCommandParserBase
    {
        public override string CommandLineHeader => "MET_DEF";

        public override void ParseMetaInfo(string line, OngekiFumen fumen)
        {
            var dataArr = ParserUtils.GetDataArray<int>(line);

            fumen.MetaInfo.MeterDefinition = new FumenMetaInfo.MetDef()
            {
                Bunshi = dataArr.ElementAtOrDefault(0),
                Bunbo = dataArr.ElementAtOrDefault(1),
            };
        }
    }
}
