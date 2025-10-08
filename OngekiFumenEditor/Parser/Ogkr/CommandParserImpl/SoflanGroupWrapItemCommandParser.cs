using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OngekiFumenEditor.Base.EditorObjects;

namespace OngekiFumenEditor.Parser.Ogkr.CommandParserImpl
{
    [Export(typeof(ICommandParser))]
    public class SoflanGroupWrapItemCommandParser : CommandParserBase
    {
        public override string CommandLineHeader => "[SGWI]";

        public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var groupName = args.GetData<string>(1);
            var itemGroup = fumen.IndividualSoflanAreaMap.SoflanGroupWrapItemGroupRoot.Children.OfType<SoflanGroupWrapItemGroup>().FirstOrDefault(x => x.DisplayName == groupName) ?? new SoflanGroupWrapItemGroup()
            {
                DisplayName = groupName
            };

            var arr = args.GetDataArray<int>().Skip(2);

            foreach (var soflanGroupId in arr)
            {
                var item = /*new SoflanGroupWrapItem(soflanGroupId)*/fumen.IndividualSoflanAreaMap.TryGetOrCreateSoflanGroupWrapItem(soflanGroupId, out _);
                item.Parent?.Remove(item);
                itemGroup.Add(item);
            }

            fumen.IndividualSoflanAreaMap.SoflanGroupWrapItemGroupRoot.Add(itemGroup);

            //nothing return
            return default;
        }
    }
}
