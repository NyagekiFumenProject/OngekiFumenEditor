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
    class TutorialCommandParser : MetaInfoCommandParserBase
    {
        public override string CommandLineHeader => "TUTORIAL";

        public override void ParseMetaInfo(string line, OngekiFumen fumen)
        {
            fumen.MetaInfo.Tutorial = ParserUtils.GetDataArray<int>(line).ElementAtOrDefault(0) == 1;
        }
    }
}
