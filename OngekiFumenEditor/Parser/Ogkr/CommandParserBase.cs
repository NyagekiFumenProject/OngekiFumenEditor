using OngekiFumenEditor.Base;

namespace OngekiFumenEditor.Parser.Ogkr
{
    public abstract class CommandParserBase : ICommandParser
    {
        public abstract string CommandLineHeader { get; }

        public virtual void AfterParse(OngekiObjectBase obj, OngekiFumen fumen) { }

        public abstract OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen);

        /// <summary>
        /// 记录「列数不足」缺陷。游戏侧按列索引读取这些字段，缺列即越界崩溃；
        /// 编辑器这里只记缺陷，调用方决定是跳过该记录还是用默认值继续。
        /// </summary>
        protected void ReportColumnTooFew(OngekiFumen fumen, CommandArgs args, int requiredColumns, int actualColumns)
        {
            ReportParseIssue(fumen, new FumenParseIssue
            {
                Kind = FumenParseIssueKind.RecordColumnTooFew,
                Tag = CommandLineHeader,
                Line = args.Line?.Trim() ?? string.Empty,
                RequiredColumns = requiredColumns,
                ActualColumns = actualColumns,
            });
        }

        /// <summary>
        /// 记录「轨道延伸段找不到起点」缺陷：游戏侧会为该 recID 注册一条 recID=-1 的残缺轨道。
        /// </summary>
        protected void ReportMissingLaneStart(OngekiFumen fumen, CommandArgs args, int recordId)
        {
            ReportParseIssue(fumen, new FumenParseIssue
            {
                Kind = FumenParseIssueKind.LaneStartNotFound,
                Tag = CommandLineHeader,
                Line = args.Line?.Trim() ?? string.Empty,
                Detail = recordId.ToString(),
            });
        }

        /// <summary>
        /// 记录「弹种 ID 重复」缺陷：游戏侧字典 Add 重复键会抛异常（读谱崩溃）。
        /// </summary>
        protected void ReportBulletPalleteIdDuplicate(OngekiFumen fumen, CommandArgs args, string strId)
        {
            ReportParseIssue(fumen, new FumenParseIssue
            {
                Kind = FumenParseIssueKind.BulletPalleteIdDuplicate,
                Tag = CommandLineHeader,
                Line = args.Line?.Trim() ?? string.Empty,
                Detail = strId,
            });
        }

        private static void ReportParseIssue(OngekiFumen fumen, FumenParseIssue issue)
        {
            fumen?.ReportParseIssue(issue);
        }
    }
}
