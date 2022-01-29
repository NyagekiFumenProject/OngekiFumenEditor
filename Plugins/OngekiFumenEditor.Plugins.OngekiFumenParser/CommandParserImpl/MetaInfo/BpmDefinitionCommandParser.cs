using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
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
    class BpmDefinitionCommandParser : MetaInfoCommandParserBase
    {
        public override string CommandLineHeader => "BPM_DEF";

        public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
        {
            var dataArr = args.GetDataArray<double>();

            fumen.MetaInfo.BpmDefinition = new FumenMetaInfo.BpmDef()
            {
                First = dataArr.ElementAtOrDefault(1),
                Common = dataArr.ElementAtOrDefault(2),
                Minimum = dataArr.ElementAtOrDefault(3),
                Maximum = dataArr.ElementAtOrDefault(4),
            };
        }
    }
}
