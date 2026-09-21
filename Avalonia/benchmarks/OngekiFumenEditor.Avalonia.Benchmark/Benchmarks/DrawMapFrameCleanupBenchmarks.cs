using BenchmarkDotNet.Attributes;

namespace OngekiFumenEditor.Avalonia.Benchmark.Benchmarks;

/// <summary>
/// 审计项 RND-C3（P2）—— 帧末 <c>drawMap.Clear()</c> 被调用两次，删除 <c>End:</c> 标签处那次。
///
/// 对照现状：
///   src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs
///     修复前 :680  finally 内 drawMap.Clear()      （成功路径 + 异常路径）
///             :690  End: 标签处 drawMap.Clear()    （本项删掉的那次）
///     修复后 :680  finally 内 drawMap.Clear()      保留（注释声明为「唯一一处帧末清理」）
///             :690  End: 标签处                        只留 builder?.Dispose() / CurrentDrawingTargetContext = default;
///
/// 被删那一次调用作用在**什么状态**的字典上（基准建模的依据，也是前两版建模错的地方）：
///   - 正常出帧：本帧填充发生在 try 内 → finally 里 <b>先</b> 逐个 Dispose 内层、<b>再</b> drawMap.Clear()
///     → CommitHitObjects()（不触碰 drawMap）→ 落入 End:。**中间没有任何代码重新填充 drawMap**，
///     所以 End: 那次 Clear 清的是「刚被 finally 清空的字典」—— 空字典。
///   - try 前早退（:314 限帧丢弃 / :346 fumen 为 null）：drawMap 本帧从未被触碰 —— 空字典。
///   - try 内抛异常：finally 执行后异常上抛，**不会**到达 End:。
///   即：被删的那一行在所有可达路径上都是 <c>Dictionary.Clear()</c> 作用于**空字典**，
///   这是 <see cref="Isolate_ClearOnEmptyMap"/> 单独量它的原因。
///
/// 历史成因（git 考古）：62957153（2026-09-05, perf(avalonia): reduce allocations in editor drawing）
///   把原先「成功路径内联」的清理搬进新引入的 finally 并新增了 finally 里的 drawMap.Clear()，
///   但没有删掉 End: 处旧有的那次；而该 commit 里 try 已位于两个 goto End 之后，
///   所以它从写下那一刻起就是死代码。**不是** RND-C2（5de442d4c）造成的 ——
///   RND-C2 只是把限帧闸门进一步提到 builder 创建之前（闸门仍在 try 之前）。
///
/// 量纲说明（重要）：
///   报告值 = <see cref="FramesPerInvoke"/> 帧（= 256 次出帧清理）的合计，单帧 = ÷ 256。
///   本项**不是热点**（审计原文自述「单次 Clear() … 两倍仍然很小」），基准的定位是把
///   「确实很小」变成有数字的结论；**不是**整帧加速比 —— 出帧的其余工作两种实现完全相同。
///
/// 设计说明（三次迭代的踩坑记录，留此以免重犯）：
///   单次 <c>Dictionary.Clear()</c> 只有几到几十纳秒，低于 BDN 计时噪声，直接量测不出来。
///   - 第一版：被测区间里「BuildMap 重建 + 清 1/2 次」→ ~250 µs 的重建分配开销淹没信号，
///     且 GC 抖动让方向都测反（ratio 0.68–1.31 乱跳）。
///   - 第二版：填充移到 <c>[IterationSetup]</c> → BDN 因该特性强制
///     <c>RunStrategy=ColdStart / InvocationCount=1</c>，每迭代仅一个样本，
///     StdDev 达均值 20–50%，仍不可读（ratio 0.94–1.46）。
///   - 第三版（当时以为对）：被测区间「BuildMap + 清 N 次」且把 End: 的 Clear 建模在
///     **满字典**上 → 与真实控制流矛盾（见上），测的是不存在的开销，ratio 依旧乱跳。
///   - **最终版**： faithful 建模 + 无分配循环。字典只建一次，<c>Clear()</c> 保留容量，
///     帧间用 <see cref="RestoreOuter"/> 把同样的 (target → inner) 条目重新挂回外层
///     （纯 Dictionary 赋值，无任何分配）；两个变体的 restore 完全相同（公共项抵消），
///     唯一差异 = Original 每帧多一次「对空字典 Clear」（正是被删的那一行）。
///     预期结论是 ratio ≈ 1.00（差异小于噪声）——**这本身就是发现**：
///     该行在帧尺度上测不出成本，删除它的价值是消除死代码与消除一处易被误解的控制流，
///     而不是可测的提速。被删行的绝对成本由 <see cref="Isolate_ClearOnEmptyMap"/> 给出。
///
/// 保真度说明：
///   用**合成复刻**而非生产类型 —— 生产 drawMap 的 value 是
///   IPooledDictionary&lt;DrawingTargetContext, IPooledList&lt;OngekiObjectBase&gt;&gt;，
///   要真实填充它必须先构造 OngekiObjectBase 子物体并走完整条绘制目标分发链路，
///   那样测到的将是「对象枚举与分发」而非「字典清理」。故只复刻被删/保留两行所操作的
///   数据结构本身（同类型参数的 Dictionary&lt;K,V&gt;）与它们在该时点的填充状态。
///   覆盖边界：不含内层 PooledDictionary 的 Dispose/归还成本（两种实现都要付、作差抵消），
///   也不含 drawMap 的填充与枚举（两种实现完全相同）。
/// </summary>
[MemoryDiagnoser]
public class DrawMapFrameCleanupBenchmarks
{
    /// <summary>每次 invocation 处理的「帧」数。报告值为这些帧的合计，单帧 = ÷ 此值。</summary>
    private const int FramesPerInvoke = 256;

    /// <summary>绘制目标数量。生产观测约 20–40，取 16/32/64 覆盖并外推。</summary>
    [Params(16, 32, 64)]
    public int TargetCount;

    /// <summary>每个绘制目标下的帧内上下文（soflan 组）数量。</summary>
    [Params(1, 4)]
    public int ContextsPerTarget;

    /// <summary>
    /// 外层字典（对应生产 drawMap）：只建一次，帧间 Clear() + <see cref="RestoreOuter"/> 复用。
    /// <c>Dictionary.Clear()</c> 保留容量，故稳态循环零分配。
    /// </summary>
    private Dictionary<FakeTarget, Dictionary<FakeContext, object>> outerMap = null!;

    /// <summary>每个绘制目标的内层字典（对应帧内 soflan 组 → 对象列表），填一次终身复用、永不清空。</summary>
    private Dictionary<FakeContext, object>[] innerMaps = null!;

    /// <summary>供 <see cref="Isolate_ClearOnEmptyMap"/> 使用的空字典（等价于被删行看到的 drawMap 状态）。</summary>
    private readonly Dictionary<FakeTarget, Dictionary<FakeContext, object>> emptyMap = new();

    private FakeTarget[] targets = null!;
    private FakeContext[] contexts = null!;

    [GlobalSetup]
    public void Setup()
    {
        targets = Enumerable.Range(0, TargetCount).Select(i => new FakeTarget(i)).ToArray();
        contexts = Enumerable.Range(0, ContextsPerTarget).Select(i => new FakeContext(i)).ToArray();

        innerMaps = new Dictionary<FakeContext, object>[TargetCount];
        for (var t = 0; t < TargetCount; t++)
        {
            var inner = new Dictionary<FakeContext, object>(ContextsPerTarget);
            for (var c = 0; c < ContextsPerTarget; c++)
                inner[contexts[c]] = new object();
            innerMaps[t] = inner;
        }

        outerMap = new Dictionary<FakeTarget, Dictionary<FakeContext, object>>(TargetCount);
        RestoreOuter();

        VerifyNewAndOldAgree();
    }

    /// <summary>
    /// 把本帧的 (target → inner) 条目重新挂回外层字典，模拟「本帧绘制阶段把 drawMap 填满」。
    /// 纯字典赋值、无分配；内层字典内容不变（生产中 finally 只把它们从外层摘下并 Dispose 归还，
    /// 本基准不模拟归还成本 —— 两种实现都要付，作差抵消）。
    /// </summary>
    private void RestoreOuter()
    {
        for (var t = 0; t < TargetCount; t++)
            outerMap[targets[t]] = innerMaps[t];
    }

    /// <summary>
    /// 新旧实现的对拍：跑完一帧后 drawMap 的终态必须一致（都为空）。
    /// 这是本项改动的**核心主张** —— 删掉重复 Clear() 不改变任何帧的可观测状态。
    /// 在 <c>[GlobalSetup]</c> 里跑，因此 <c>--job dry</c> 也会执行，等价性不必等正式跑完即可验证。
    /// </summary>
    private void VerifyNewAndOldAgree()
    {
        for (var targetCount = 0; targetCount <= TargetCount; targetCount++)
        for (var contextCount = 0; contextCount <= ContextsPerTarget; contextCount++)
        {
            // --- 旧实现：finally 清一次（满字典）+ End: 再清一次（此时已为空，忠实于 finally→End 的顺序）---
            var oldMap = BuildMap(targetCount, contextCount);
            oldMap.Clear();      // 复刻 :680 finally
            oldMap.Clear();      // 复刻 :690 End: —— 作用在刚被清空的字典上

            // --- 新实现：只清 finally 那一处 ---
            var newMap = BuildMap(targetCount, contextCount);
            newMap.Clear();      // 复刻 :680 finally

            if (oldMap.Count != newMap.Count || newMap.Count != 0)
                throw new InvalidOperationException(
                    $"RND-C3 等价性失败：targetCount={targetCount}, contextCount={contextCount} 时" +
                    $" old.Count={oldMap.Count}, new.Count={newMap.Count}（都应为 0）。");

            // 早退帧（goto End）时 drawMap 未被本帧填充：End: 对从未填充的空字典 Clear 也是空操作。
            var earlyExitMap = new Dictionary<FakeTarget, Dictionary<FakeContext, object>>();
            earlyExitMap.Clear();
            if (earlyExitMap.Count != 0)
                throw new InvalidOperationException("RND-C3 等价性失败：早退帧的空 drawMap 清理后不为空。");
        }
    }

    /// <summary>等价性自检用的小构造器（不在被测区间内）。</summary>
    private static Dictionary<FakeTarget, Dictionary<FakeContext, object>> BuildMap(
        int targetCount, int contextCount)
    {
        var m = new Dictionary<FakeTarget, Dictionary<FakeContext, object>>(targetCount);
        for (var t = 0; t < targetCount; t++)
        {
            var inner = new Dictionary<FakeContext, object>(contextCount);
            for (var c = 0; c < contextCount; c++)
                inner[new FakeContext(c)] = new object();
            m[new FakeTarget(t)] = inner;
        }

        return m;
    }

    /// <summary>
    /// 修复前（基线）：每帧「填满 → finally 清（满字典）→ End: 再清（空字典）」，
    /// 忠实复刻 :680 与 :690 的执行顺序与各自作用其上的字典状态。
    /// </summary>
    [Benchmark(Baseline = true)]
    public int Original_ClearTwice()
    {
        var sum = 0;
        for (var f = 0; f < FramesPerInvoke; f++)
        {
            RestoreOuter();
            outerMap.Clear();   // :680 finally（满字典）
            outerMap.Clear();   // :690 End:（空字典 ← 本项删掉的那次）
            sum += outerMap.Count;
        }

        return sum;
    }

    /// <summary>修复后：每帧「填满 → finally 清（满字典）」，End: 处不再重复。</summary>
    [Benchmark]
    public int Optimized_ClearOnce()
    {
        var sum = 0;
        for (var f = 0; f < FramesPerInvoke; f++)
        {
            RestoreOuter();
            outerMap.Clear();   // :680 finally（满字典）
            sum += outerMap.Count;
        }

        return sum;
    }

    /// <summary>
    /// 隔离项：被删那一行的**绝对成本** —— <c>Dictionary.Clear()</c> 作用于空字典
    /// （正常出帧时它是「刚被 finally 清空」，早退帧时它是「从未填充」，均为空字典）。
    /// 与上面两项同量纲（同帧数）。预期 ~0.5–1 ns/帧、零分配：
    /// 这就是「该行为什么从来没做过任何事」的数字答案。
    /// </summary>
    [Benchmark]
    public int Isolate_ClearOnEmptyMap()
    {
        var sum = 0;
        for (var f = 0; f < FramesPerInvoke; f++)
        {
            emptyMap.Clear();
            sum += emptyMap.Count;
        }

        return sum;
    }

    /// <summary>绘制目标占位（生产侧为 IFumenEditorDrawingTarget 引用，逐帧稳定）。</summary>
    public sealed class FakeTarget(int id)
    {
        public int Id { get; } = id;

        public override int GetHashCode() => Id;
    }

    /// <summary>帧内上下文占位（生产侧为 DrawingTargetContext）。</summary>
    public sealed class FakeContext(int id)
    {
        public int Id { get; } = id;

        public override int GetHashCode() => Id;
    }
}
