using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Base.OngekiObjects
{
    /// <summary>
    /// Hold 判定 tick 步长的唯一实现。
    /// <para>
    /// 原先这份公式在两个地方各写了一份——<see cref="Hold.CalculateJudgeTGrid"/> 用 log2 闭式、
    /// FumenStatisticsCalculator 用倍增循环。后者在 PROGJUDGE_BPM（或 BPM）取到 0 时循环条件永远不翻转，
    /// 结果就是导出/转换/绘制时的静默卡死（PERF-PRS-001 / PRS-01）。这里收敛成一份并补上终止性保证。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 不变量：<see cref="Calculate"/> 的返回值恒 &gt;= 1。调用方“逐 tick 推进直到追上终点”的循环因此一定终止。
    /// </remarks>
    public static class HoldTickStepCalculator
    {
        /// <summary>PROGJUDGE_BPM 的默认值，与 FumenMetaInfo.ProgJudgeBpm 的默认值保持一致。</summary>
        public const float DefaultProgJudgeBpm = 240f;

        /// <summary>
        /// PROGJUDGE_BPM 的合法下界（临界值）：小于它的一律判为非法输入，含 0、负数、NaN 与无穷。
        /// <para>
        /// 取 1 的依据：判定 BPM 小于 1 拍/分钟没有意义；且与审核清单里对同类边界项的处置口径一致
        /// （DAT-01「校验 1..1920」、AUD-05「clamp 至 1」），都以下界 1 作为“最小值”。
        /// </para>
        /// </summary>
        public const float MinProgJudgeBpm = 1f;

        /// <summary>默认分辨率下的 1/4 切片长度（1920 / 4 = 480）。</summary>
        public const int DefaultStandardBeatLen = (int)(TGrid.DEFAULT_RES_T / 4);

        /// <summary>判断一个 PROGJUDGE_BPM 是否为合法输入（有限且 &gt;= <see cref="MinProgJudgeBpm"/>）。</summary>
        public static bool IsValidProgJudgeBpm(float value)
            => float.IsFinite(value) && value >= MinProgJudgeBpm;

        /// <summary>
        /// 把（可能非法的）PROGJUDGE_BPM 收敛成合法值：非法时报错并回落 <see cref="DefaultProgJudgeBpm"/>。
        /// 解析与 UI 入口都应经过这里，避免非法值一路带到 tick 步长计算。
        /// </summary>
        public static float CoerceProgJudgeBpm(float value)
        {
            if (IsValidProgJudgeBpm(value))
                return value;

            Log.LogError($"Invalid PROGJUDGE_BPM '{value}': expected a finite value >= {MinProgJudgeBpm}. Fallback to {DefaultProgJudgeBpm}.");
            return DefaultProgJudgeBpm;
        }

        /// <summary>
        /// 计算 1/4 切片在 <paramref name="standardBeatLen"/> 基础上的缩放步长。
        /// </summary>
        /// <param name="bpm">该 tick 处的 BPM。</param>
        /// <param name="progressJudgeBpm">判定用 BPM，即 PROGJUDGE_BPM。</param>
        /// <param name="standardBeatLen">1/4 切片长度，默认 <see cref="DefaultStandardBeatLen"/>。</param>
        /// <returns>恒 &gt;= 1 的步长。</returns>
        public static int Calculate(double bpm, double progressJudgeBpm, int standardBeatLen)
        {
            if (standardBeatLen < 1)
                standardBeatLen = 1;

            // 只有两边都是正的有限值，缩放才有意义；否则保持 shift = 0（返回基准步长）。
            // 这样 0、负数、NaN、Inf 都会落到一个安全且非 0 的步长上，而不是让循环失去推进性。
            var shift = 0;
            if (bpm > 0 && progressJudgeBpm > 0 && double.IsFinite(bpm) && double.IsFinite(progressJudgeBpm))
            {
                if (bpm < progressJudgeBpm)
                    shift = -(int)Math.Ceiling(Math.Log2(progressJudgeBpm / bpm));
                else
                    shift = (int)Math.Floor(Math.Log2(bpm / progressJudgeBpm));
            }

            // 用 long 移位避免 int 溢出，再夹到 [1, int.MaxValue]：
            // 右移过量（比值超出基准的位宽）-> 1；左移过量 -> int.MaxValue，避免溢出成负数让推进反向。
            var step = shift >= 0
                ? (long)standardBeatLen << (int)Math.Min(shift, 31)
                : (long)standardBeatLen >> (int)Math.Min(-(long)shift, 63);

            return (int)Math.Clamp(step, 1L, int.MaxValue);
        }
    }
}
