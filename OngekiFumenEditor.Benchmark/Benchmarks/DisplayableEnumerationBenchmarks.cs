using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Benchmark.Infrastructure;

namespace OngekiFumenEditor.Benchmark.Benchmarks;

/// <summary>
/// 可显示对象枚举(plan.md 第 2 条点名了 ConnectableStartObject.GetDisplayableObjects())。
/// 覆盖三种枚举入口:全谱、范围内、ConnectableStart 逐个。
/// </summary>
public class DisplayableEnumerationBenchmarks : FumenBenchmarkBase
{
    [Benchmark]
    [STAThread]
    public int GetAllDisplayableObjects()
    {
        var count = 0;
        foreach (var _ in PrimaryFumen.GetAllDisplayableObjects())
            count++;
        return count;
    }

    [Benchmark]
    [STAThread]
    public int GetAllDisplayableObjectsInRange()
    {
        var count = 0;
        foreach (var _ in PrimaryFumen.GetAllDisplayableObjects(RangeMin, RangeMax))
            count++;
        return count;
    }

    [Benchmark(Baseline = true)]
    [STAThread]
    public int ConnectableStartGetDisplayableObjects()
    {
        var count = 0;
        foreach (var start in ConnectableStarts)
        {
            foreach (var _ in start.GetDisplayableObjects())
                count++;
        }
        return count;
    }

    // 方案 A 对比候选:展开 LINQ 链 + 修复每个 child 被 yield 两次。
    [Benchmark]
    [STAThread]
    public int ConnectableStartGetDisplayableObjectsFast()
    {
        var count = 0;
        foreach (var start in ConnectableStarts)
        {
            foreach (var _ in start.GetDisplayableObjectsFast())
                count++;
        }
        return count;
    }
}
