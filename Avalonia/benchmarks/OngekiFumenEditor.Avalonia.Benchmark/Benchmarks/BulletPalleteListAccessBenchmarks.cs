using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;

namespace OngekiFumenEditor.Avalonia.Benchmark.Benchmarks;

/// <summary>
/// PERF-DAT-009 / DAT-12 — <c>BulletPalleteList</c> 的 int 索引器无限递归，且每次枚举都重新 OrderBy。
///
/// 对照（09-09 修复前基线）: Avalonia/src/OngekiFumenEditor.Avalonia/Base/Collections/BulletPalleteList.cs:42-44
///   - <c>public BulletPallete this[int index] =&gt; this[index];</c> —— 索引器调用自身，任何 int 索引
///     读取都会以不可捕获的 StackOverflowException 结束进程。
///   - <c>GetEnumerator() =&gt; palleteMap.Values.OrderBy(x =&gt; ConvertIdToInt(x.StrID)).GetEnumerator()</c>
///     —— 每次枚举都物化并排序一遍，且每个元素都要跑一次 ConvertIdToInt（ToUpperInvariant + Reverse +
///     Select + Sum，多次分配）。调用方不少：SelectionFilterOptions.UpdateOptions、EditorProjectDataUtils
///     的 Apply/Store、两个 formatter，以及解析期的 BulletCommandParser/BellCommandParser 逐条命令
///     `FirstOrDefault(x =&gt; x.StrID == ...)`。
///
/// 修复（本 benchmark 的参考实现）: 维护一个按 ConvertIdToInt(StrID) 升序的 backing List，索引器、Count
/// 与枚举都走它；插入用二分定位。这样 (1) 索引器不再递归且顺序与枚举一致，(2) 枚举不再排序、不再分配
/// 排序缓冲，(3) 仍与旧 <c>OrderBy</c> 的数值序完全等价。
///
/// 量纲: 每个 [Params] 组合是一个独立规模点，报告值是**单次调用**（枚举一次 / 按索引走一遍 / 处理一整条
/// 命令流），不是整帧或整文件耗时。旧实现按 OrderBy 重排建模（索引器无法真的调用——会崩）。
/// </summary>
[MemoryDiagnoser]
public class BulletPalleteListEnumerationBenchmarks
{
    /// <summary>调色板数量。既是枚举规模，也是命令流长度（每个规模点各查一次）。</summary>
    [Params(8, 64, 256)]
    public int PalleteCount { get; set; }

    private Dictionary<int, BulletPallete> legacyMap = null!;
    private BulletPalleteList list = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var data = BulletPalleteListBenchmarkData.Build(PalleteCount);
        legacyMap = data.Legacy;
        list = data.Real;
    }

    /// <summary>旧实现：每次枚举都 <c>OrderBy(ConvertIdToInt)</c> 物化 + 排序。</summary>
    [Benchmark(Baseline = true)]
    public int Original_Enumerate()
    {
        var acc = 0;
        foreach (var pallete in legacyMap.Values.OrderBy(x => BulletPalleteList.ConvertIdToInt(x.StrID)))
            acc += pallete.StrID.Length;
        return acc;
    }

    /// <summary>新实现：直接枚举有序 backing 列表，无排序、无排序缓冲分配。</summary>
    [Benchmark]
    public int Optimized_Enumerate()
    {
        var acc = 0;
        foreach (var pallete in list)
            acc += pallete.StrID.Length;
        return acc;
    }
}

/// <summary>
/// PERF-DAT-009 / DAT-12 —— <c>IReadOnlyList&lt;BulletPallete&gt;</c> 的按下标读取。
///
/// 旧实现里这条路径直接崩（索引器自递归），所以这里把“修好递归但不动结构”的最小补丁建为基线：
/// 每次按下标取值都 <c>OrderBy(...).ElementAt(i)</c>，即 O(n log n) 重排 + 排序缓冲分配。修复后是
/// 有序列表的 O(1) 索引。读取模式取 <c>for (i = 0; i &lt; Count; i++)</c> 顺序走一遍——这正是
/// IReadOnlyList 消费者（绑定/序列化器/视图层）会做的事。
/// </summary>
[MemoryDiagnoser]
public class BulletPalleteListIndexedAccessBenchmarks
{
    [Params(8, 64, 256)]
    public int PalleteCount { get; set; }

    private Dictionary<int, BulletPallete> legacyMap = null!;
    private BulletPalleteList list = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var data = BulletPalleteListBenchmarkData.Build(PalleteCount);
        legacyMap = data.Legacy;
        list = data.Real;
    }

    /// <summary>最小补丁（修掉递归但保留按需重排）：每个下标都重排一次。</summary>
    [Benchmark(Baseline = true)]
    public int Original_IndexByReordering()
    {
        var acc = 0;
        for (var i = 0; i < list.Count; i++)
            acc += legacyMap.Values.OrderBy(x => BulletPalleteList.ConvertIdToInt(x.StrID)).ElementAt(i).StrID.Length;
        return acc;
    }

    /// <summary>修复后的实现：有序 backing 列表的 O(1) 索引器。</summary>
    [Benchmark]
    public int Optimized_Index()
    {
        var acc = 0;
        for (var i = 0; i < list.Count; i++)
            acc += list[i].StrID.Length;
        return acc;
    }
}

/// <summary>
/// PERF-DAT-009 / DAT-12 —— 解析期的逐命令调色板查找（<c>FirstOrDefault(x =&gt; x.StrID == id)</c>）。
///
/// 这是该缺陷现实中真正被反复踩到的路径：<c>BulletCommandParser.cs:23</c> 与 <c>BellCommandParser.cs:25</c>
/// （nyageki 与 ogkr 两套）对**每一条** bullet/bell 命令都做一次全列表查找，而旧实现的每次枚举都要重排 +
/// 每个元素跑一次 ConvertIdToInt，成本是 命令数 × (n log n + ConvertIdToInt 分配)。
///
/// 命令流按 3:1 混合命中与未命中，避免只剩下“一定找得到”的分支。
/// </summary>
[MemoryDiagnoser]
public class BulletPalleteListLookupBenchmarks
{
    [Params(8, 64, 256)]
    public int PalleteCount { get; set; }

    private Dictionary<int, BulletPallete> legacyMap = null!;
    private BulletPalleteList list = null!;
    private string[] commandIds = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var data = BulletPalleteListBenchmarkData.Build(PalleteCount);
        legacyMap = data.Legacy;
        list = data.Real;
        commandIds = data.CommandIds;
    }

    /// <summary>旧实现：逐命令重排后线性查找。</summary>
    [Benchmark(Baseline = true)]
    public int Original_ParserStyleLookup()
    {
        var acc = 0;
        foreach (var id in commandIds)
        {
            var hit = legacyMap.Values
                .OrderBy(x => BulletPalleteList.ConvertIdToInt(x.StrID))
                .FirstOrDefault(x => x.StrID == id);
            acc += hit is null ? -1 : hit.StrID.Length;
        }
        return acc;
    }

    /// <summary>新实现：线性查找但不再重排（查找本身的 O(n) 未动，见审计条目的「未做/后续」）。</summary>
    [Benchmark]
    public int Optimized_ParserStyleLookup()
    {
        var acc = 0;
        foreach (var id in commandIds)
        {
            var hit = list.FirstOrDefault(x => x.StrID == id);
            acc += hit is null ? -1 : hit.StrID.Length;
        }
        return acc;
    }
}

/// <summary>
/// 三个基准类共用的夹具：同一批调色板同时喂给“旧的 map + OrderBy”和“新的有序列表”，
/// 保证两侧看到完全相同的 id 集合与相同的插入顺序（乱序插入，迫使 backing 列表真的走二分定位）。
/// </summary>
internal static class BulletPalleteListBenchmarkData
{
    internal sealed record Fixture(
        Dictionary<int, BulletPallete> Legacy,
        BulletPalleteList Real,
        string[] StrIds,
        string[] CommandIds);

    internal static Fixture Build(int count)
    {
        var ids = new int[count];
        for (var i = 0; i < count; i++)
            ids[i] = i + 1;

        // 固定种子的确定性洗牌：乱序插入，且 id 唯一（每次都是新增而非同 id 替换）。
        var rng = new Random(20260914);
        for (var i = ids.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (ids[i], ids[j]) = (ids[j], ids[i]);
        }

        var legacy = new Dictionary<int, BulletPallete>();
        var real = new BulletPalleteList();
        var strIds = new string[count];

        for (var i = 0; i < count; i++)
        {
            var strId = BulletPalleteList.ConvertIntToId(ids[i]);
            strIds[i] = strId;
            legacy[BulletPalleteList.ConvertIdToInt(strId)] = new BulletPallete { StrID = strId };
            real.AddPallete(new BulletPallete { StrID = strId });
        }

        // 命令流：3/4 命中已存在的 id，1/4 落在集合外，避免只测“必然命中”的分支。
        var commands = new string[count];
        for (var i = 0; i < count; i++)
            commands[i] = i % 4 == 3
                ? BulletPalleteList.ConvertIntToId(count + i + 1)
                : strIds[rng.Next(count)];

        return new Fixture(legacy, real, strIds, commands);
    }
}
