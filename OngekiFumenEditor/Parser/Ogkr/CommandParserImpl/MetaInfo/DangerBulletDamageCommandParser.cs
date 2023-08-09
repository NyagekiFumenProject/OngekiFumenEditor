using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using OngekiFumenEditor.Parser;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl.MetaInfo
{
    [Export(typeof(ICommandParser))]
    class DangerBulletDamageCommandParser : MetaInfoCommandParserBase
    {
        public override string CommandLineHeader => "DANGERBULLET_DAMAGE";

        public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
        {
            fumen.MetaInfo.DangerBulletDamage = args.GetData<double>(1);
        }
    }
}
