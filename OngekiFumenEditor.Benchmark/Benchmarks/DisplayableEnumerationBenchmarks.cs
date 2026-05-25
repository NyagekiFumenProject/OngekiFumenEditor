using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Benchmark.Infrastructure;

namespace OngekiFumenEditor.Benchmark.Benchmarks;

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
    public int ConnectableGetDisplayableObjects()
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
