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
    class ProgJudgeBpmCommandParsers : MetaInfoCommandParserBase
    {
        public override string CommandLineHeader => "PROGJUDGE_BPM";

        public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
        {
            fumen.MetaInfo.BulletDamage = args.GetData<double>(1);
        }
    }
}
