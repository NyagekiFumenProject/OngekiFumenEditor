using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using OngekiFumenEditor.Parser;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditorPlugins.OngekiFumenSupport.CommandParserImpl.MetaInfo
{
    [Export(typeof(ICommandParser))]
    class HardBulletDamageCommandParser : MetaInfoCommandParserBase
    {
        public override string CommandLineHeader => "HARDBULLET_DAMAGE";

        public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
        {
            fumen.MetaInfo.HardBulletDamage = args.GetData<double>(1);
        }
    }
}
