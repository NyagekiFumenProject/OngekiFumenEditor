namespace OngekiFumenEditor.Parser.Ogkr
{
    public class FumenStatisticsResult
    {
        public int BellObjects { get; set; }
        public int FlickObjects { get; set; }
        public int SideObjects { get; set; }
        public int TapObjects { get; internal set; }
        public int SideHoldObjects { get; internal set; }
        public int TotalObjects { get; internal set; }
        public int HoldObjects { get; internal set; }

        /// <summary>
        /// 白金分数上限。游戏侧每个判定单元最多给 2 分（Counters.addPlatinumScore / updatePlatinumScore），
        /// 且 8 个 NoteType 槽位的 volumeNotesTech 权重全为 1，因此上限 = TotalObjects × 2，
        /// 与 DataManager 读取的 header.totalNotesNum × 2 一致。
        /// </summary>
        public int PlatinumScoreMax { get; internal set; }

        /// <summary>Hold 持续判定点数量（T_HOLD + T_SHOLD，含终点判定、不含 Hold 起点）。</summary>
        public int HoldTickObjects { get; internal set; }

        /// <summary><see cref="HoldTickObjects"/> 占 <see cref="TotalObjects"/> 的比例（0..1；无判定单元时为 0）。</summary>
        public double HoldTickShare { get; internal set; }
    }
}
