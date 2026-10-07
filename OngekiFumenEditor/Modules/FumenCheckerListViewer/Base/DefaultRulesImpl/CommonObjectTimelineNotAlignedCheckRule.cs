using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    [Export(typeof(IFumenCheckRule))]
    internal class CommonObjectTimelineNotAlignedCheckRule : IFumenCheckRule
    {
        /// <summary>
        /// 「对上节奏」的判定标准：物件相对拍起点的偏移量与一拍长度的最大公因数达到该网格数，即视为
        /// 落在规整格点上。4/4 下 10 网格 = 1/48 拍 = 1/192 音符（190 BPM 约 6ms），比实际谱面用法都细，
        /// 因此三连音/五连音、3/8、5/8、7/12 这类 k/n 位置都能通过，只有真正离格的偏移才会被报出来。
        /// </summary>
        private const long MinLatticeSpacingGrids = 10;

        /// <summary>
        /// 相对下限：格点不得细于 1/48 拍。与绝对下限取宽的那一个生效 —— 4/4 下两者相等（10 网格），
        /// 短拍号（如 15/4，一拍仅 128 网格）下放宽到 1/48 拍，避免把 3/4、15/16 拍这类位置误报成离格。
        /// </summary>
        private const long MinLatticeDenominator = 48;

        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            const string ruleName = "ObjectTimelineNotAligned";

            // BPM ≤ 0 时拍段列表的时间轴换算会算出 NaN，TimeSpan 会直接抛异常（见 BpmOutOfRange 规则）。
            // 这种谱面的节奏基准已经不成立，直接跳过本规则，避免一条规则把整批检查结果打崩。
            if (fumen.BpmList.Any(x => x.BPM < 0.0001))
                yield break;

            var beats = fumen.MeterChanges.GetCachedAllTimeSignatureUniformPositionList(fumen.BpmList);
            var currentIndex = 0;
            var currentStartTGrid = default(TGrid);
            var currentMeter = default(MeterChange);
            var nextStartTGrid = default(TGrid);

            void UpdateStatus()
            {
                (_, currentStartTGrid, currentMeter, _) = beats[currentIndex];
                nextStartTGrid = beats.ElementAtOrDefault(currentIndex + 1).startTGrid;
            }

            UpdateStatus();

            foreach (var obj in fumen.Taps.OrderBy(x => x.TGrid))
            {
                while (nextStartTGrid != null && obj.TGrid >= nextStartTGrid)
                {
                    currentIndex++;
                    UpdateStatus();
                }

                // 判定全走整数：旧的浮点判等（1 / trck 再和取整比较）在拍序号很大时会因精度丢失把
                // 规整位置算成 3.0000000000000426 之类的值而误报；旧的 {1/n, (n-1)/n} 允许集也表达不了
                // k/n 这类位置（3/8、5/8、7/12……）。取偏移与拍长的最大公因数即可覆盖全部规整 k/n 格点。
                var beatTotalGrid = GetBeatTotalGrid(currentMeter);
                var offsetInBeat = ((long)obj.TGrid.TotalGrid - currentStartTGrid.TotalGrid) % beatTotalGrid;
                if (offsetInBeat < 0)
                    offsetInBeat += beatTotalGrid;
                if (offsetInBeat == 0)
                    continue;

                var spacing = MathUtils.GCD((int)beatTotalGrid, (int)offsetInBeat);
                if (spacing >= MinLatticeSpacingGrids || spacing * MinLatticeDenominator >= beatTotalGrid)
                    continue;

                yield return new CommonCheckResult
                {
                    Severity = RuleSeverity.Problem,
                    Description = Resources.ObjectTimelineNotAligned.Format(obj.IDShortName),
                    LocationDescription = $"{obj}",
                    NavigateBehavior = new NavigateToObjectBehavior(obj),
                    RuleName = ruleName,
                };
            }
        }

        /// <summary>
        /// 一拍的长度（网格 = ResT / BunShi）。真实谱面里存在 0/4、7/8 这类分子为 0 或不整除 ResT 的拍号，
        /// 此时得不到整数拍长，退化为整个 TGrid 单位长度，等价于按绝对格点判断。
        /// </summary>
        private static long GetBeatTotalGrid(MeterChange meter)
        {
            if (meter is { BunShi: > 0 } && TGrid.DEFAULT_RES_T % meter.BunShi == 0)
                return TGrid.DEFAULT_RES_T / meter.BunShi;
            return TGrid.DEFAULT_RES_T;
        }
    }
}

