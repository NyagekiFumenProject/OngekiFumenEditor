namespace OngekiFumenEditor.Benchmark.Infrastructure.ResultComparison;

public sealed record BenchmarkBaseline(
    int SchemaVersion,
    string BenchmarkClass,
    DateTime SavedAtUtc,
    string[] MachineInfo,
    string Runtime,
    Dictionary<string, MethodMetrics> Methods)
{
    public const int CurrentSchemaVersion = 1;
}
