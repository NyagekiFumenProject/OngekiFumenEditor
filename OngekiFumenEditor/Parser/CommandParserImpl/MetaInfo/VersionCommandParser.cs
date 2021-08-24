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
    class VersionCommandParser : MetaInfoCommandParserBase
    {
        public override string CommandLineHeader => "VERSION";

        public override void ParseMetaInfo(string line, OngekiFumen fumen)
        {
            var dataArr = ParserUtils.GetDataArray<int>(line);
            fumen.MetaInfo.Version = new Version(dataArr.ElementAtOrDefault(0), dataArr.ElementAtOrDefault(1), dataArr.ElementAtOrDefault(2));
        }
    }
}
