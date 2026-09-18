namespace OngekiFumenEditor.Benchmark.Baselines;

public sealed record BenchmarkBaseline(
    int SchemaVersion,
    string BenchmarkClass,
    DateTime SavedAtUtc,
    string[] MachineInfo,
    string Runtime,
    Dictionary<string, MethodMetric> Methods)
{
    public const int CurrentSchemaVersion = 1;
}
