using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models;
using System;
using System.ComponentModel.Composition;
using System.Globalization;

namespace OngekiFumenEditor.Parser.Ogkr.CommandParserImpl
{
    /// <summary>旧版 [SGC]（读取迁移）：恢复组文件夹。</summary>
    [Export(typeof(ICommandParser))]
    public sealed class SoflanGroupCategoryCommandParser : CommandParserBase
    {
        public override string CommandLineHeader => "[SGC]";

        public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var tree = fumen.IndividualSoflanAreaMap.SoflanGroupTree;
            var path = SoflanGroupTreeSerialization.DecodePath(args.GetData<string>(1));
            var category = tree.GetOrCreateCategory(path);

            if (int.TryParse(args.GetData<string>(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var order))
                tree.Move(category, category.Parent, order);

            return default;
        }
    }

    /// <summary>旧版 [SGF]（读取迁移）：恢复组 ID/名称/归属。</summary>
    [Export(typeof(ICommandParser))]
    public sealed class SoflanGroupCommandParser : CommandParserBase
    {
        public override string CommandLineHeader => "[SGF]";

        public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            if (!int.TryParse(args.GetData<string>(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                return default;

            var tree = fumen.IndividualSoflanAreaMap.SoflanGroupTree;
            var group = tree.GetOrCreateGroup(id);
            group.Name = SoflanGroupTreeSerialization.DecodeName(args.GetData<string>(4));

            var parentPath = args.GetData<string>(2);
            var parent = string.IsNullOrEmpty(parentPath) || parentPath == SoflanGroupTreeSerialization.RootPath
                ? tree.Root
                : tree.GetOrCreateCategory(SoflanGroupTreeSerialization.DecodePath(parentPath));

            if (!group.IsDefault)
            {
                if (int.TryParse(args.GetData<string>(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out var order))
                    tree.Move(group, parent, order);
                else
                    tree.Move(group, parent);
            }

            tree.MarkLayoutDefined(group);
            return default;
        }
    }
}
