namespace OngekiFumenEditor.Benchmark.Baselines;

public sealed record MethodMetric(
    double MeanNs,
    double StandardErrorNs,
    double StandardDeviationNs,
    long? AllocatedBytes,
    long? Gen0Collections,
    long TotalOperations);
