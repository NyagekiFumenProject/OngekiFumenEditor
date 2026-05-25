using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Benchmark.Infrastructure;
using OngekiFumenEditor.Kernel.CurveInterpolater.OgkrImpl.Factory;
using CoreInterpolateAll = OngekiFumenEditor.Utils.Ogkr.InterpolateAll;

namespace OngekiFumenEditor.Benchmark.Benchmarks;

public class CurvePathBenchmarks : FumenBenchmarkBase
{
    [Benchmark]
    [STAThread]
    public int GetConnectionPaths()
    {
        var count = 0;
        foreach (var child in CurvedChildren)
            count += child.GetConnectionPaths().Count;
        return count;
    }

    [Benchmark]
    [STAThread]
    public int GenAllPath()
    {
        var count = 0;
        foreach (var start in CurvedStarts)
        {
            foreach (var _ in start.GenAllPath())
                count++;
        }

        return count;
    }

    [Benchmark]
    [STAThread]
    public int InterpolateCurve()
    {
        var count = 0;
        foreach (var start in CurvedStarts)
            count += start.InterpolateCurve(XGridLimitedCurveInterpolaterFactory.Default).Count();
        return count;
    }

    [Benchmark]
    [STAThread]
    public int InterpolateAllCalculate()
    {
        var count = 0;
        foreach (var (_, genStarts) in CoreInterpolateAll.Calculate(PrimaryFumen, XGridLimitedCurveInterpolaterFactory.Default))
            count += genStarts.Count();
        return count;
    }
}
