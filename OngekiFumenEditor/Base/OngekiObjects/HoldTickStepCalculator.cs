using OngekiFumenEditor.Utils;
using System;
using System.Globalization;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    /// <summary>
    /// Hold 判定 tick 步长的统一计算实现。
    /// </summary>
    /// <remarks>
    /// 返回值始终至少为 1，确保逐 tick 推进的循环不会因为非法 BPM 失去推进性。
    /// </remarks>
    public static class HoldTickStepCalculator
    {
        /// <summary>PROGJUDGE_BPM 的默认值。</summary>
        public const float DefaultProgJudgeBpm = 240f;

        /// <summary>判定 BPM 的最小允许值。</summary>
        public const float MinProgJudgeBpm = 1f;

        /// <summary>默认分辨率下的 1/4 切片长度。</summary>
        public const int DefaultStandardBeatLen = (int)(TGrid.DEFAULT_RES_T / 4);

        public static bool IsValidProgJudgeBpm(float value)
            => float.IsFinite(value) && value >= MinProgJudgeBpm;

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
                Log.LogError($"Invalid PROGJUDGE_BPM '{value}': expected a finite value >= {MinProgJudgeBpm}. Fallback to {DefaultProgJudgeBpm}.");
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
        public static int Calculate(double bpm, double progressJudgeBpm, int standardBeatLen)
        {
            if (standardBeatLen < 1)
                standardBeatLen = 1;

            var shift = 0;
            if (bpm > 0 && progressJudgeBpm > 0 && double.IsFinite(bpm) && double.IsFinite(progressJudgeBpm))
            {
                if (bpm < progressJudgeBpm)
                    shift = -(int)Math.Ceiling(Math.Log2(progressJudgeBpm / bpm));
                else
                    shift = (int)Math.Floor(Math.Log2(bpm / progressJudgeBpm));
            }

            var step = shift >= 0
                ? (long)standardBeatLen << (int)Math.Min(shift, 31)
                : (long)standardBeatLen >> (int)Math.Min(-(long)shift, 63);

            return (int)Math.Clamp(step, 1L, int.MaxValue);
        }
    }
}
