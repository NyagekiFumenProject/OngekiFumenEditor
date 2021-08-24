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
    class DangerBulletDamageCommandParser : MetaInfoCommandParserBase
    {
        public override string CommandLineHeader => "DANGERBULLET_DAMAGE";

        public override void ParseMetaInfo(string line, OngekiFumen fumen)
        {
            fumen.MetaInfo.DangerBulletDamage = ParserUtils.GetDataArray<double>(line).ElementAtOrDefault(0);
        }
    }
}
