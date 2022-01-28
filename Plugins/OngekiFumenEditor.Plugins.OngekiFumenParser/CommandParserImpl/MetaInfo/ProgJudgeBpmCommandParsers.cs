using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using OngekiFumenEditor.Parser;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditorPlugins.OngekiFumenParser.CommandParserImpl.MetaInfo
{
    [Export(typeof(ICommandParser))]
    class ProgJudgeBpmCommandParsers : MetaInfoCommandParserBase
    {
        public override string CommandLineHeader => "PROGJUDGE_BPM";

        public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
        {
            fumen.MetaInfo.ProgJudgeBpm = args.GetData<float>(1);
        }
    }
}
