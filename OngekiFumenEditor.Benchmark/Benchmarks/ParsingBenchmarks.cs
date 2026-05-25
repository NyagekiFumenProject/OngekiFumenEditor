using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Benchmark.Infrastructure;

namespace OngekiFumenEditor.Benchmark.Benchmarks;

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
    public int DeserializeOgkrSamples() => DeserializeSamples(ogkrSamples);

    [Benchmark]
    [STAThread]
    public int DeserializeNyagekiSamples() => DeserializeSamples(nyagekiSamples);

    [Benchmark]
    [STAThread]
    public int DeserializeAllSamples() => DeserializeSamples(allSamples);

    private static int DeserializeSamples(IEnumerable<FumenSample> samples)
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
