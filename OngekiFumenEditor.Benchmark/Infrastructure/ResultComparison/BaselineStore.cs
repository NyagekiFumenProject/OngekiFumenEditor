using System.IO;
using System.Text.Json;

namespace OngekiFumenEditor.Benchmark.Infrastructure.ResultComparison;

public static class BaselineStore
{
    private const string ProjectFileName = "OngekiFumenEditor.Benchmark.csproj";
    private const string ArtifactsDirName = "BenchmarkDotNet.Artifacts";
    private const string BaselinesDirName = "Baselines";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static string? _cachedBaselinesDir;

    public static string GetBaselinesDirectory()
    {
        if (_cachedBaselinesDir is not null)
            return _cachedBaselinesDir;

        var projectRoot = TryFindProjectRoot();
        if (projectRoot is null)
        {
            Console.WriteLine($"[Baseline] Warning: could not locate {ProjectFileName} relative to '{AppContext.BaseDirectory}'; falling back to current directory for baselines.");
            projectRoot = Directory.GetCurrentDirectory();
        }

        _cachedBaselinesDir = Path.Combine(projectRoot, ArtifactsDirName, BaselinesDirName);
        return _cachedBaselinesDir;
    }

    public static BenchmarkBaseline? Load(string benchmarkClassFullName)
    {
        var path = GetBaselinePath(benchmarkClassFullName);
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            var baseline = JsonSerializer.Deserialize<BenchmarkBaseline>(json, JsonOptions);
            if (baseline is null)
                return null;

            if (baseline.SchemaVersion != BenchmarkBaseline.CurrentSchemaVersion)
            {
                Console.WriteLine($"[Baseline] Skipping '{path}': schema version {baseline.SchemaVersion} != expected {BenchmarkBaseline.CurrentSchemaVersion}.");
                return null;
            }

            return baseline;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Baseline] Failed to read baseline '{path}': {ex.Message}");
            return null;
        }
    }

    public static void Save(BenchmarkBaseline baseline)
    {
        var dir = GetBaselinesDirectory();
        Directory.CreateDirectory(dir);

        var path = GetBaselinePath(baseline.BenchmarkClass);
        var json = JsonSerializer.Serialize(baseline, JsonOptions);
        File.WriteAllText(path, json);
        Console.WriteLine($"[Baseline] Saved -> {path}");
    }

    private static string GetBaselinePath(string benchmarkClassFullName)
        => Path.Combine(GetBaselinesDirectory(), $"{benchmarkClassFullName}.json");

    private static string? TryFindProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, ProjectFileName)))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
