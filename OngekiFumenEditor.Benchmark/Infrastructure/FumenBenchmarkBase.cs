using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;

namespace OngekiFumenEditor.Benchmark.Infrastructure;

public abstract class FumenBenchmarkBase
{
    protected IReadOnlyList<ParsedFumenSample> Samples { get; private set; } = Array.Empty<ParsedFumenSample>();
    protected OngekiFumen PrimaryFumen { get; private set; } = null!;
    protected TGrid RangeMin { get; private set; } = TGrid.Zero;
    protected TGrid RangeMax { get; private set; } = TGrid.FromTotalGrid((int)TGrid.DEFAULT_RES_T * 8);
    protected IReadOnlyList<ConnectableStartObject> ConnectableStarts { get; private set; } = Array.Empty<ConnectableStartObject>();
    protected IReadOnlyList<ConnectableChildObjectBase> ConnectableChildren { get; private set; } = Array.Empty<ConnectableChildObjectBase>();
    protected IReadOnlyList<ConnectableStartObject> CurvedStarts { get; private set; } = Array.Empty<ConnectableStartObject>();
    protected IReadOnlyList<ConnectableChildObjectBase> CurvedChildren { get; private set; } = Array.Empty<ConnectableChildObjectBase>();

    [GlobalSetup]
    public virtual void GlobalSetup()
    {
        BenchmarkRuntime.EnsureInitialized();

        Samples = SampleCorpus.ParsedSamples;
        if (Samples.Count == 0)
            throw new InvalidOperationException("No parsed fumen samples are available.");

        PrimaryFumen = Samples
            .OrderByDescending(x => x.Fumen.GetAllDisplayableObjects().Count())
            .First()
            .Fumen;

        ConnectableStarts = PrimaryFumen.Lanes.Cast<ConnectableStartObject>()
            .Concat(PrimaryFumen.Beams.Cast<ConnectableStartObject>())
            .ToArray();
        ConnectableChildren = ConnectableStarts.SelectMany(x => x.Children).ToArray();
        CurvedChildren = ConnectableChildren.Where(x => x.PathControls.Count > 0 || !x.IsVaildPath).ToArray();
        CurvedStarts = ConnectableStarts.Where(x => x.Children.Any(c => c.PathControls.Count > 0) || !x.IsPathVaild()).ToArray();

        var maxTotalGrid = Math.Max(
            (int)TGrid.DEFAULT_RES_T * 16,
            Samples.SelectMany(x => x.Fumen.GetAllDisplayableObjects().OfType<ITimelineObject>())
                .Select(x => x.TGrid.TotalGrid)
                .DefaultIfEmpty(0)
                .Max());
        var span = Math.Max((int)TGrid.DEFAULT_RES_T * 4, maxTotalGrid / 8);
        var center = maxTotalGrid / 2;

        RangeMin = TGrid.FromTotalGrid(Math.Max(0, center - span / 2));
        RangeMax = TGrid.FromTotalGrid(center + span / 2);
    }
}
