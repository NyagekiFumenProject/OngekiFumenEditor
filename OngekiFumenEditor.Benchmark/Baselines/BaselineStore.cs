using System.IO;
using System.Text.Json;

namespace OngekiFumenEditor.Benchmark.Baselines;

/// <summary>
/// 将 baseline 序列化到 BenchmarkDotNet.Artifacts/Baselines/{ClassFullName}.json。
/// 文件名以 benchmark 类全名为基础,plan.md 第 5 条要求。
/// </summary>
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

    private static string? cachedBaselinesDir;

    public static string GetBaselinesDirectory()
    {
        if (cachedBaselinesDir is not null)
            return cachedBaselinesDir;

        var projectRoot = TryFindProjectRoot();
        if (projectRoot is null)
        {
            Console.WriteLine(
                $"[Baseline] Warning: could not locate {ProjectFileName} relative to '{AppContext.BaseDirectory}'; "
                + "falling back to current working directory.");
            projectRoot = Directory.GetCurrentDirectory();
        }

        cachedBaselinesDir = Path.Combine(projectRoot, ArtifactsDirName, BaselinesDirName);
        return cachedBaselinesDir;
    }

    public static string GetBaselinePath(string benchmarkClassFullName)
        => Path.Combine(GetBaselinesDirectory(), $"{benchmarkClassFullName}.json");

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
                Console.WriteLine(
                    $"[Baseline] Ignoring '{path}': schema version {baseline.SchemaVersion} "
                    + $"!= expected {BenchmarkBaseline.CurrentSchemaVersion}.");
                return null;
            }

            return baseline;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Baseline] Failed to read '{path}': {ex.Message}");
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
