using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Benchmark.Infrastructure;

namespace OngekiFumenEditor.Benchmark.Benchmarks;

public class CollectionQueryBenchmarks : FumenBenchmarkBase
{
    [Benchmark]
    [STAThread]
    public int BinaryFindRangeOnSortedCollections()
    {
        var count = 0;
        count += PrimaryFumen.Taps.BinaryFindRange(RangeMin, RangeMax).Count();
        count += PrimaryFumen.Flicks.BinaryFindRange(RangeMin, RangeMax).Count();
        count += PrimaryFumen.Bells.BinaryFindRange(RangeMin, RangeMax).Count();
        count += PrimaryFumen.Bullets.BinaryFindRange(RangeMin, RangeMax).Count();
        count += PrimaryFumen.ClickSEs.BinaryFindRange(RangeMin, RangeMax).Count();
        count += PrimaryFumen.EnemySets.BinaryFindRange(RangeMin, RangeMax).Count();
        count += PrimaryFumen.Comments.BinaryFindRange(RangeMin, RangeMax).Count();
        return count;
    }

    [Benchmark]
    [STAThread]
    public int GetVisibleStartObjects()
    {
        var count = 0;
        count += PrimaryFumen.Lanes.GetVisibleStartObjects(RangeMin, RangeMax).Count();
        count += PrimaryFumen.Holds.GetVisibleStartObjects(RangeMin, RangeMax).Count();
        count += PrimaryFumen.Beams.GetVisibleStartObjects(RangeMin, RangeMax).Count();
        count += PrimaryFumen.LaneBlocks.GetVisibleStartObjects(RangeMin, RangeMax).Count();
        return count;
    }

    [Benchmark]
    [STAThread]
    public int SoflanIntervalQueries()
    {
        var count = 0;
        foreach (var soflans in PrimaryFumen.SoflansMap.Values)
            count += soflans.GetVisibleStartObjects(RangeMin, RangeMax).Count();

        foreach (var individualSoflanAreas in PrimaryFumen.IndividualSoflanAreaMap.Values)
            count += individualSoflanAreas.GetVisibleStartObjects(RangeMin, RangeMax).Count();

        return count;
    }
}
