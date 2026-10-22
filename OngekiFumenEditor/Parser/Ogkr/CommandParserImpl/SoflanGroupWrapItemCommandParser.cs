using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Parser.Ogkr.CommandParserImpl
{
    /// <summary>
    /// 旧格式 [SGWI] &lt;name&gt; &lt;id...&gt; 兼容解析。
    /// 旧版 [SGC]/[SGF] 定义过的组（HasExplicitLayout）不会被 SGWI 重新挂载。
    /// </summary>
    [Export(typeof(ICommandParser))]
    public class SoflanGroupWrapItemCommandParser : CommandParserBase
    {
        public override string CommandLineHeader => "[SGWI]";

        public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var tree = fumen.IndividualSoflanAreaMap.SoflanGroupTree;
            var groupName = args.GetData<string>(1) ?? string.Empty;
            var category = groupName == "default" || string.IsNullOrEmpty(groupName)
                ? tree.Root
                : tree.GetOrCreateCategory(new[] { groupName });

            foreach (var soflanGroupId in args.GetDataArray<int>().Skip(2))
            {
                var group = tree.GetGroup(soflanGroupId) ?? tree.GetOrCreateGroup(soflanGroupId);
                if (group.HasExplicitLayout)
                    continue;

                if (!group.IsDefault)
                    tree.Move(group, category);
                tree.MarkLayoutDefined(group);
            }

            return default;
        }
    }
}
