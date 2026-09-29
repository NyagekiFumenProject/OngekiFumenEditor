using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;

namespace OngekiFumenEditor.Benchmark.Benchmarks;

/// <summary>
/// 预览模式打击特效的 Hold 判定刻度枚举（性能报告 §8 P2 的落地验证）。
///
/// 旧实现每一步推进都 `curTGrid = curTGrid + new GridOffset(0, tickGrid)`：
/// 每步分配 1×GridOffset(24 B) + 1×TGrid(56 B)，而 300 ms 预览窗口之外的 skip 步骤占绝大多数，
/// 这些对象全是垃圾（实测该站点 424 MB/75 s，占采样分配总量 ~5%）。
/// 新实现用私有累计实例原地推进（TGrid.AddOffset），只在 yield 点复制 TGrid。
///
/// 本类自建 Hold/BpmList（不依赖 IoC）；Legacy 分支逐行复刻旧迭代器（含旧 GridOffset 的 class 分配形态），
/// Setup 对「窗口 × ProgJudgeBpm」矩阵逐步对拍 (Unit, Grid, TotalGrid, TotalUnit) 的位级一致性。
/// 一次操作 = 一遍完整枚举。
/// </summary>
public class HoldJudgeTickEnumerationBenchmarks
{
    private const int Beat = (int)TGrid.DEFAULT_RES_T;
    private const int HoldBeats = 256;
    private const int PreviewWindowGrid = 2304; // 300 ms @ 240 BPM
    private const int InvokeCount = 8;
    private const float ProgressJudgeBpm = 240;

    private Hold hold = null!;
    private BpmList bpmList = null!;

    private TGrid previewMin = null!;
    private TGrid previewMax = null!;
    private TGrid fullMin = null!;
    private TGrid fullMax = null!;

    /// <summary>旧 GridOffset（record class）的分配形态复刻。</summary>
    private sealed record LegacyGridOffset(float Unit, int Grid);

    [GlobalSetup]
    public void Setup()
    {
        hold = new Hold
        {
            TGrid = TGrid.FromTotalGrid(0),
        };
        hold.SetHoldEnd(new HoldEnd
        {
            TGrid = TGrid.FromTotalGrid(HoldBeats * Beat),
        });

        bpmList = new BpmList();
        bpmList.Add(new BPMChange { BPM = 180, TGrid = TGrid.FromTotalGrid(200 * Beat) });
        bpmList.Add(new BPMChange { BPM = 300, TGrid = TGrid.FromTotalGrid(224 * Beat) });

        // 代表现场：Hold 早就开始，预览窗口落在第一段 BPM 中间 → skip 步占绝对多数
        previewMin = TGrid.FromTotalGrid(100 * Beat);
        previewMax = TGrid.FromTotalGrid(100 * Beat + PreviewWindowGrid);

        fullMin = TGrid.FromTotalGrid(0);
        fullMax = TGrid.FromTotalGrid((HoldBeats + 2) * Beat);

        ValidateEquivalence();
    }

    private void ValidateEquivalence()
    {
        var windows = new (TGrid min, TGrid max)[]
        {
            (fullMin, fullMax),
            (previewMin, previewMax),
            (TGrid.FromTotalGrid(30 * Beat), TGrid.FromTotalGrid(31 * Beat)),
            (TGrid.FromTotalGrid(90 * Beat), TGrid.FromTotalGrid(91 * Beat)),
            (TGrid.FromTotalGrid(210 * Beat), TGrid.FromTotalGrid(210 * Beat + PreviewWindowGrid)),
            (TGrid.FromTotalGrid(0), TGrid.FromTotalGrid(Beat)),
        };

        foreach (var progressJudgeBpm in new[] { 240f, 960f, 120f })
            foreach (var (minTGrid, maxTGrid) in windows)
                AssertSameSequence(minTGrid, maxTGrid, progressJudgeBpm);
    }

    private void AssertSameSequence(TGrid minTGrid, TGrid maxTGrid, float progressJudgeBpm)
    {
        using var legacy = EnumerateLegacy(hold, minTGrid, maxTGrid, bpmList, progressJudgeBpm).GetEnumerator();
        using var current = hold.CalculateJudgeTGrid(minTGrid, maxTGrid, bpmList, progressJudgeBpm).GetEnumerator();

        var index = 0;
        while (true)
        {
            var legacyHasNext = legacy.MoveNext();
            var currentHasNext = current.MoveNext();

            if (legacyHasNext != currentHasNext)
                throw new InvalidOperationException(
                    $"序列长度不一致(第 {index} 项): legacy={legacyHasNext}, current={currentHasNext}, 窗口=[{minTGrid},{maxTGrid}], progJudgeBpm={progressJudgeBpm}");

            if (!legacyHasNext)
                return;

            var expected = legacy.Current;
            var actual = current.Current;

            if (BitConverter.SingleToInt32Bits(expected.Unit) != BitConverter.SingleToInt32Bits(actual.Unit)
                || expected.Grid != actual.Grid
                || expected.TotalGrid != actual.TotalGrid
                || BitConverter.DoubleToInt64Bits(expected.TotalUnit) != BitConverter.DoubleToInt64Bits(actual.TotalUnit))
                throw new InvalidOperationException(
                    $"第 {index} 项不一致(窗口=[{minTGrid},{maxTGrid}], progJudgeBpm={progressJudgeBpm}): legacy={expected}, current={actual}");

            index++;
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = InvokeCount)]
    [STAThread]
    public double JudgeTicks_Legacy_JumpToPreview() => RunLegacy(previewMin, previewMax);

    [Benchmark(OperationsPerInvoke = InvokeCount)]
    [STAThread]
    public double JudgeTicks_InPlace_JumpToPreview() => RunInPlace(previewMin, previewMax);

    [Benchmark(OperationsPerInvoke = InvokeCount)]
    [STAThread]
    public double JudgeTicks_Legacy_FullEnumeration() => RunLegacy(fullMin, fullMax);

    [Benchmark(OperationsPerInvoke = InvokeCount)]
    [STAThread]
    public double JudgeTicks_InPlace_FullEnumeration() => RunInPlace(fullMin, fullMax);

    private double RunLegacy(TGrid minTGrid, TGrid maxTGrid)
    {
        var acc = 0d;
        for (var i = 0; i < InvokeCount; i++)
            foreach (var tGrid in EnumerateLegacy(hold, minTGrid, maxTGrid, bpmList, ProgressJudgeBpm))
                acc += tGrid.TotalGrid;

        return acc;
    }

    private double RunInPlace(TGrid minTGrid, TGrid maxTGrid)
    {
        var acc = 0d;
        for (var i = 0; i < InvokeCount; i++)
            foreach (var tGrid in hold.CalculateJudgeTGrid(minTGrid, maxTGrid, bpmList, ProgressJudgeBpm))
                acc += tGrid.TotalGrid;

        return acc;
    }

    /// <summary>旧 <c>Hold.CalculateJudgeTGrid</c> 的逐行复刻（含旧分配形态）。</summary>
    private static IEnumerable<TGrid> EnumerateLegacy(Hold hold, TGrid minTGrid, TGrid maxTGrid, BpmList bpmList, float progressJudgeBpm)
    {
        int CalcHoldTickStepSize(double bpm)
        {
            var standardBeatLen = TGrid.DEFAULT_RES_T / 4;

            if (bpm < progressJudgeBpm)
            {
                var ratio = progressJudgeBpm / bpm;
                var power = (int)Math.Ceiling(Math.Log(ratio, 2));
                standardBeatLen >>= power;
            }
            else
            {
                var ratio = bpm / progressJudgeBpm;
                var power = (int)Math.Floor(Math.Log(ratio, 2));
                standardBeatLen <<= power;
            }

            return (int)standardBeatLen;
        }

        var holdStartTGrid = hold.TGrid;
        var holdEndTGrid = hold.HoldEnd?.TGrid;
        if (holdEndTGrid is null)
            yield break;

        var curTGrid = holdStartTGrid;

        while (curTGrid < holdEndTGrid)
        {
            var bpm = bpmList.GetBpm(curTGrid);
            var nextTGrid = bpmList.GetNextBpm(curTGrid)?.TGrid ?? TGrid.MaxValue;

            //minTGrid is between this bpm and the next, so we could start to enumerate them from this bpm
            if (bpm.TGrid <= minTGrid && minTGrid <= nextTGrid)
            {
                var tickGrid = CalcHoldTickStepSize(bpm.BPM);
                curTGrid = AdvanceLegacy(curTGrid, new LegacyGridOffset(0, tickGrid));

                //skip to minTGrid
                while (curTGrid < minTGrid)
                {
                    tickGrid = CalcHoldTickStepSize(bpm.BPM);
                    curTGrid = AdvanceLegacy(curTGrid, new LegacyGridOffset(0, tickGrid));
                }

                //enumerate until hold end or maxTGrid
                while (curTGrid < holdEndTGrid && curTGrid < maxTGrid)
                {
                    yield return curTGrid;

                    bpm = bpmList.GetBpm(curTGrid);
                    tickGrid = CalcHoldTickStepSize(bpm.BPM);
                    curTGrid = AdvanceLegacy(curTGrid, new LegacyGridOffset(0, tickGrid));
                }

                //finally check if need to yield hold end
                if (maxTGrid >= holdEndTGrid && curTGrid >= holdEndTGrid)
                    yield return holdEndTGrid;

                break;
            }
            else
            {
                //not in range yet, skip curTGrid to relative pos of next bpm
                var tickGrid = CalcHoldTickStepSize(bpm.BPM);
                var nextBpmCurTGrid = AdvanceLegacy(curTGrid, new LegacyGridOffset(0, tickGrid * ((nextTGrid.TotalGrid - curTGrid.TotalGrid) / tickGrid + 1)));

                curTGrid = nextBpmCurTGrid;
            }
        }
    }

    private static TGrid AdvanceLegacy(TGrid l, LegacyGridOffset r)
    {
        var unit = l.Unit + r.Unit;
        var grid = r.Grid + l.Grid;

        while (grid < 0)
        {
            unit -= 1;
            grid = (int)(grid + l.ResT);
        }

        unit += grid / l.ResT;
        grid = (int)(grid % l.ResT);

        return new TGrid(unit, grid);
    }
}
