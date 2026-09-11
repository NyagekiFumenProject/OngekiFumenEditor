using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Avalonia.Benchmark.Infrastructure;

namespace OngekiFumenEditor.Avalonia.Benchmark.Benchmarks;

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

    [Benchmark]
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
}
