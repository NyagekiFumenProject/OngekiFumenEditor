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
    class ClickDefinitionCommandParser : MetaInfoCommandParserBase
    {
        public override string CommandLineHeader => "CLK_DEF";

        public override void ParseMetaInfo(string line, OngekiFumen fumen)
        {
            var dataArr = ParserUtils.GetDataArray<int>(line);
            fumen.MetaInfo.ClickDefinition = dataArr.ElementAtOrDefault(0);
        }
    }
}
