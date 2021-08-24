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
    class XResolutionCommandParser : MetaInfoCommandParserBase
    {
        public override string CommandLineHeader => "XRESOLUTION";

        public override void ParseMetaInfo(string line, OngekiFumen fumen)
        {
            var dataArr = ParserUtils.GetDataArray<int>(line);
            fumen.MetaInfo.XRESOLUTION = dataArr.ElementAtOrDefault(0);
        }
    }
}
