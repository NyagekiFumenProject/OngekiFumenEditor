using OngekiFumenEditor.Base;
using OngekiFumenEditor.Parser;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditorPlugins.OngekiFumenSupport.CommandParserImpl.MetaInfo
{
    [Export(typeof(ICommandParser))]
    class ClickDefinitionCommandParser : MetaInfoCommandParserBase
    {
        public override string CommandLineHeader => "CLK_DEF";

        public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
        {
            fumen.MetaInfo.ClickDefinition = args.GetData<int>(1);
        }
    }
}
