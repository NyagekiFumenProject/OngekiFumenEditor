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
    class BeamDamageCommandParsers : MetaInfoCommandParserBase
    {
        public override string CommandLineHeader => "BEAM_DAMAGE";

        public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
        {
            fumen.MetaInfo.BeamDamage = args.GetData<float>(1);
        }
    }
}
