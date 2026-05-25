namespace OngekiFumenEditor.Benchmark.Infrastructure.ResultComparison;

public enum ComparisonStatus
{
    Ok,
    New,
    Gone,
    Regression,
    Improved,
    Unknown
}

public sealed record ComparisonResult(
    string Method,
    MethodMetrics? Previous,
    MethodMetrics? Current,
    ComparisonStatus MeanStatus,
    ComparisonStatus AllocStatus,
    double? MeanDeltaPercent,
    double? AllocDeltaPercent);
