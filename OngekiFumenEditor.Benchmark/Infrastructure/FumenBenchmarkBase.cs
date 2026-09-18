using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;

namespace OngekiFumenEditor.Benchmark.Infrastructure;

/// <summary>
/// 给 benchmark 类提供已解析好的 OngekiFumen 与衍生集合。每个 benchmark 子类继承后
/// 自动获得 PrimaryFumen / RangeMin/RangeMax / ConnectableStarts 等开箱即用的数据。
/// </summary>
public abstract class FumenBenchmarkBase
{
    protected IReadOnlyList<ParsedFumenSample> Samples { get; private set; } = Array.Empty<ParsedFumenSample>();
    protected OngekiFumen PrimaryFumen { get; private set; } = null!;
    protected TGrid RangeMin { get; private set; } = TGrid.Zero;
    protected TGrid RangeMax { get; private set; } = TGrid.FromTotalGrid((int)TGrid.DEFAULT_RES_T * 8);
    protected IReadOnlyList<ConnectableStartObject> ConnectableStarts { get; private set; } = Array.Empty<ConnectableStartObject>();

    [GlobalSetup]
    public virtual void GlobalSetup()
    {
        BenchmarkRuntime.EnsureInitialized();

        Samples = SampleCorpus.ParsedSamples;
        if (Samples.Count == 0)
            throw new InvalidOperationException("No parsed fumen samples available.");

        // 挑选可显示对象数量最多的一份谱面作为主样本,让 benchmark 数据规模有代表性。
        PrimaryFumen = Samples
            .OrderByDescending(s => s.Fumen.GetAllDisplayableObjects().Count())
            .First()
            .Fumen;

        ConnectableStarts = PrimaryFumen.Lanes.Cast<ConnectableStartObject>()
            .Concat(PrimaryFumen.Beams.Cast<ConnectableStartObject>())
            .ToArray();

        // 范围取整张谱面时间轴的中间一段,避免端点空数据。
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
