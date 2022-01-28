using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using OngekiFumenEditor.Parser;
using System.Threading.Tasks;

namespace OngekiFumenEditorPlugins.OngekiFumenParser.CommandParserImpl.MetaInfo
{
    [Export(typeof(ICommandParser))]
    class TutorialCommandParser : MetaInfoCommandParserBase
    {
        public override string CommandLineHeader => "TUTORIAL";

        public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
        {
            fumen.MetaInfo.Tutorial = args.GetData<int>(1) == 1;
        }
    }
}
