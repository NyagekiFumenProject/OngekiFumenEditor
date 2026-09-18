using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Benchmark.Infrastructure;

namespace OngekiFumenEditor.Benchmark.Benchmarks;

/// <summary>
/// 谱面解析性能(plan.md 第 2 条):测 OGKR / Nyageki / 全部样本的反序列化吞吐。
/// </summary>
public class ParsingBenchmarks
{
    private IReadOnlyList<FumenSample> ogkrSamples = Array.Empty<FumenSample>();
    private IReadOnlyList<FumenSample> nyagekiSamples = Array.Empty<FumenSample>();
    private IReadOnlyList<FumenSample> allSamples = Array.Empty<FumenSample>();

    [GlobalSetup]
    public void GlobalSetup()
    {
        BenchmarkRuntime.EnsureInitialized();
        ogkrSamples = SampleCorpus.OgkrSamples;
        nyagekiSamples = SampleCorpus.NyagekiSamples;
        allSamples = SampleCorpus.AllSamples;
    }

    [Benchmark]
    [STAThread]
    public int DeserializeOgkrSamples() => DeserializeAll(ogkrSamples);

    [Benchmark]
    [STAThread]
    public int DeserializeNyagekiSamples() => DeserializeAll(nyagekiSamples);

    [Benchmark]
    [STAThread]
    public int DeserializeAllSamples() => DeserializeAll(allSamples);

    private static int DeserializeAll(IEnumerable<FumenSample> samples)
    {
        var count = 0;
        foreach (var sample in samples)
        {
            var fumen = SampleCorpus.Deserialize(sample);
            count += fumen.GetAllDisplayableObjects().Count();
        }
        return count;
    }
}
