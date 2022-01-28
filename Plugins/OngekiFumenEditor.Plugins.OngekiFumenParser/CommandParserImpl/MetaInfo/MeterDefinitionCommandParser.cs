using OngekiFumenEditor.Base;
using OngekiFumenEditor.Parser;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditorPlugins.OngekiFumenParser.CommandParserImpl.MetaInfo
{
    [Export(typeof(ICommandParser))]
    class MeterDefinitionCommandParser : MetaInfoCommandParserBase
    {
        public override string CommandLineHeader => "MET_DEF";

        public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
        {
            var dataArr = args.GetDataArray<int>();

            fumen.MetaInfo.MeterDefinition = new FumenMetaInfo.MetDef()
            {
                Bunshi = dataArr.ElementAtOrDefault(1),
                Bunbo = dataArr.ElementAtOrDefault(2),
            };
        }
    }
}
