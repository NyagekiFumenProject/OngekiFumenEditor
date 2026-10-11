using OngekiFumenEditor.Utils;
using System;
using System.Globalization;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    /// <summary>
    /// Hold 判定 tick 步长的统一计算实现。
    /// </summary>
    /// <remarks>
    /// 公式对照游戏 MU3 <c>Reader/BPM.cs</c> 的 <c>getProgressJudgeGrid()</c>：
    /// BPM 与 PROGJUDGE_BPM 都以 float32 参与运算，BPM 先被抬到 3.75（<see cref="MinEffectiveBpm"/>）以上，
    /// 再以 2 的幂次缩放 1/4 切片长度；判定点序列由 <c>Reader/HoldObj.cs</c> 的 <c>CreateJudgePoint()</c> 给出。
    /// 返回值始终至少为 1，确保逐 tick 推进的循环不会因为非法 BPM 失去推进性。
    /// </remarks>
    public static class HoldTickStepCalculator
    {
        /// <summary>PROGJUDGE_BPM 的默认值。</summary>
        public const float DefaultProgJudgeBpm = 240f;

        /// <summary>
        /// PROGJUDGE_BPM 的读取阈值：不超过该值的声明按 <see cref="DefaultProgJudgeBpm"/> 处理，
        /// 与游戏 Header.decode（<c>progJudgeBPM &lt;= 1.00001f</c> 时回落 240）保持一致。
        /// </summary>
        public const float ProgJudgeBpmFallbackThreshold = 1.00001f;

        /// <summary>
        /// 参与判定步长计算的 BPM 下限，对应游戏 <c>Mathf.Max(bpm, 3.75f)</c>：
        /// 低于（或无法与）3.75 比较的 BPM 一律按 3.75 处理，NaN 同样回落到 3.75。
        /// </summary>
        public const float MinEffectiveBpm = 3.75f;

        /// <summary>默认分辨率下的 1/4 切片长度。</summary>
        public const int DefaultStandardBeatLen = (int)(TGrid.DEFAULT_RES_T / 4);

        public static bool IsValidProgJudgeBpm(float value)
            => float.IsFinite(value) && value > ProgJudgeBpmFallbackThreshold;

        public static bool TryParseProgJudgeBpm(string value, out float result)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                && IsValidProgJudgeBpm(parsed))
            {
                result = parsed;
                return true;
            }

            result = DefaultProgJudgeBpm;
            return false;
        }

        /// <summary>
        /// 将可能非法的 PROGJUDGE_BPM 收敛为安全值，避免 UI 或其他调用方绕过解析器时污染判定计算。
        /// </summary>
        public static float CoerceProgJudgeBpm(float value)
        {
            if (IsValidProgJudgeBpm(value))
                return value;

            try
            {
                Log.LogError($"Invalid PROGJUDGE_BPM '{value}': expected a finite value > {ProgJudgeBpmFallbackThreshold}. Fallback to {DefaultProgJudgeBpm}.");
            }
            catch
            {
                // Logging may not be composed yet during very early startup.
            }

            return DefaultProgJudgeBpm;
        }

        /// <summary>
        /// 计算指定 BPM 下的 Hold 判定步长，结果始终大于 0。
        /// </summary>
        /// <remarks>
        /// 与游戏 <c>MU3.Reader.BPM.getProgressJudgeGrid()</c> 的倍增循环逐位对齐：
        /// BPM/PROGJUDGE_BPM 转成 float32 比较，BPM 先抬到 <see cref="MinEffectiveBpm"/>，
        /// 再按两侧的 2 的幂次关系缩放 <paramref name="standardBeatLen"/>。
        /// 仅在 PROGJUDGE_BPM 非正或非有限时不缩放——正常路径上解析器与 UI 入口已保证其合法。
        /// 与游戏唯一的刻意差别：结果被夹在 [1, int.MaxValue]。游戏在极端输入下会把步长
        /// 右移到 0（0 步长会让它的判定点循环失去推进性）或左移溢出成负数。
        /// </remarks>
        public static int Calculate(double bpm, double progressJudgeBpm, int standardBeatLen)
        {
            if (standardBeatLen < 1)
                standardBeatLen = 1;

            var effectiveBpm = (float)bpm;
            if (!(effectiveBpm > MinEffectiveBpm))
                effectiveBpm = MinEffectiveBpm;

            var effectiveProgJudgeBpm = (float)progressJudgeBpm;

            long step = standardBeatLen;
            if (float.IsFinite(effectiveProgJudgeBpm) && effectiveProgJudgeBpm > 0f)
            {
                if (effectiveBpm < effectiveProgJudgeBpm)
                {
                    while (effectiveBpm < effectiveProgJudgeBpm)
                    {
                        step >>= 1;
                        effectiveBpm *= 2f;
                    }
                }
                else
                {
                    for (var doubled = effectiveProgJudgeBpm * 2f; doubled <= effectiveBpm; doubled *= 2f)
                    {
                        if (step > int.MaxValue >> 1)
                            return int.MaxValue;

                        step <<= 1;
                    }
                }
            }

            return (int)Math.Clamp(step, 1L, int.MaxValue);
        }
    }
}
