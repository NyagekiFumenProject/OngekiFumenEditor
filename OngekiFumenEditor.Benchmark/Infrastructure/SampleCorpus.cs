using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Parser;

namespace OngekiFumenEditor.Benchmark.Infrastructure;

public sealed record FumenSample(string FileName, string Extension, byte[] Data)
{
    public bool IsOgkr => Extension.Equals(".ogkr", StringComparison.OrdinalIgnoreCase);
    public bool IsNyageki => Extension.Equals(".nyageki", StringComparison.OrdinalIgnoreCase);
}

public sealed record ParsedFumenSample(FumenSample Sample, OngekiFumen Fumen);

internal static partial class SampleCorpus
{
    private static readonly Lazy<IReadOnlyList<FumenSample>> allSamples = new(LoadSamples);
    private static readonly Lazy<IReadOnlyList<FumenSample>> ogkrSamples =
        new(() => AllSamples.Where(x => x.IsOgkr).ToArray());
    private static readonly Lazy<IReadOnlyList<FumenSample>> nyagekiSamples =
        new(() => AllSamples.Where(x => x.IsNyageki).ToArray());
    private static readonly Lazy<IReadOnlyList<ParsedFumenSample>> parsedSamples = new(
        () => AllSamples.Select(s => new ParsedFumenSample(s, Deserialize(s))).ToArray());

    public static IReadOnlyList<FumenSample> AllSamples => allSamples.Value;
    public static IReadOnlyList<FumenSample> OgkrSamples => ogkrSamples.Value;
    public static IReadOnlyList<FumenSample> NyagekiSamples => nyagekiSamples.Value;
    public static IReadOnlyList<ParsedFumenSample> ParsedSamples => parsedSamples.Value;

    public static OngekiFumen Deserialize(FumenSample sample)
    {
        BenchmarkRuntime.EnsureInitialized();

        var parserManager = IoC.Get<IFumenParserManager>();
        var deserializer = parserManager.GetDeserializer(sample.FileName)
            ?? throw new InvalidOperationException(
                $"No deserializer registered for sample '{sample.FileName}'.");

        using var stream = new MemoryStream(sample.Data, writable: false);
        return deserializer.DeserializeAsync(stream).GetAwaiter().GetResult();
    }

    private static IReadOnlyList<FumenSample> LoadSamples()
    {
        var assembly = typeof(SampleCorpus).Assembly;
        var names = assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".Data.FumenSamples.", StringComparison.Ordinal)
                        && (n.EndsWith(".ogkr", StringComparison.OrdinalIgnoreCase)
                            || n.EndsWith(".nyageki", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        if (names.Length == 0)
            throw new InvalidOperationException(
                "No embedded fumen samples found under Data/FumenSamples/. " +
                "Check that the .csproj <EmbeddedResource> globs are intact.");

        return names.Select(name =>
        {
            var match = SampleFileNameRegex().Match(name);
            // Resource name format: <RootNamespace>.Data.FumenSamples.<file>.{ogkr|nyageki}
            // Tail group captures the original filename for parser hint matching.
            var fileName = match.Success ? match.Value : Path.GetFileName(name);

            using var stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Embedded sample stream missing: {name}");
            using var ms = new MemoryStream();
            stream.CopyTo(ms);

            return new FumenSample(fileName, Path.GetExtension(fileName), ms.ToArray());
        }).ToArray();
    }

    [GeneratedRegex(@"[^.]+\.(?:ogkr|nyageki)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SampleFileNameRegex();
}
