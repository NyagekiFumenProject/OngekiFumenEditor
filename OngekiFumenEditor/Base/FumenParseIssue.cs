using System.Collections.Generic;

namespace OngekiFumenEditor.Base
{
    /// <summary>
    /// 解析 .ogkr 时发现的「游戏侧会出错、但编辑器仍能加载」的记录级缺陷类别。
    /// </summary>
    public enum FumenParseIssueKind
    {
        /// <summary>
        /// BPL 弹种 ID 重复：游戏侧 BulletPalleteList 以 strID 为字典键，重复 Add 抛 ArgumentException
        /// （读谱直接崩溃）。
        /// </summary>
        BulletPalleteIdDuplicate,

        /// <summary>
        /// 轨道 Next/End 记录找不到对应 Start：游戏侧仍会注册该轨道（recID=-1），引用它的 tap/hold
        /// 取 laneTagTbl.laneType 时空引用，出现两条以上残缺轨道则字典重复键崩溃。
        /// </summary>
        LaneStartNotFound,

        /// <summary>
        /// 记录列数不足：游戏侧对几何/颜色字段按列索引直接读取，缺列即越界（读谱崩溃）；
        /// 编辑器侧则会把缺读到的字段静默写成默认值（回写会丢列）。
        /// </summary>
        RecordColumnTooFew,

        /// <summary>
        /// PROGJUDGE_BPM 无效（非有限数、低于允许下限或解析失败）；编辑器已回退到默认判定 BPM。
        /// </summary>
        InvalidProgJudgeBpm,
    }

    /// <summary>
    /// 一条解析期缺陷。由 <c>Parser</c> 层在发现记录无法完整表达时写入
    /// <see cref="OngekiFumen.ReportParseIssue"/>，检查规则通过 <see cref="OngekiFumen.ParseIssues"/> 读取。
    /// </summary>
    public class FumenParseIssue
    {
        public FumenParseIssueKind Kind { get; init; }

        /// <summary>记录类型（谱面行的命令名，例如 BPL / CLS / OBS）。</summary>
        public string Tag { get; init; }

        /// <summary>原始行文本（已 Trim）。</summary>
        public string Line { get; init; }

        /// <summary>实际列数（含命令名列）。不适用时为 0。</summary>
        public int ActualColumns { get; init; }

        /// <summary>游戏侧要求的最小列数（含命令名列）。不适用时为 0。</summary>
        public int RequiredColumns { get; init; }

        /// <summary>定位用的补充信息（例如 recordId、重复的弹种 ID）。</summary>
        public string Detail { get; init; }
    }
}
