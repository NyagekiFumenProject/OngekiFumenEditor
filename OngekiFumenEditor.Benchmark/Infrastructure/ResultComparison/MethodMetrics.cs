namespace OngekiFumenEditor.Benchmark.Infrastructure.ResultComparison;

public sealed record MethodMetrics(
    double MeanNs,
    double StandardErrorNs,
    double StandardDeviationNs,
    long? AllocatedBytes,
    long? Gen0Collections,
    long TotalOperations);
