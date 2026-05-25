using BenchmarkDotNet.Reports;

namespace OngekiFumenEditor.Benchmark.Infrastructure.ResultComparison;

public static class BaselineFactory
{
    public static BenchmarkBaseline? FromSummary(Summary summary)
    {
        if (summary.HasCriticalValidationErrors || summary.Reports.Length == 0)
            return null;

        var firstReport = summary.Reports[0];
        var classFullName = firstReport.BenchmarkCase.Descriptor.Type.FullName
                            ?? firstReport.BenchmarkCase.Descriptor.Type.Name;

        var methods = new Dictionary<string, MethodMetrics>();
        foreach (var report in summary.Reports)
        {
            if (!report.Success || report.ResultStatistics is null)
                continue;

            var stats = report.ResultStatistics;
            var allocated = report.GcStats.GetBytesAllocatedPerOperation(report.BenchmarkCase);
            var gen0 = report.GcStats.GetCollectionsCount(0);
            var totalOps = report.GcStats.TotalOperations;

            var metrics = new MethodMetrics(
                MeanNs: stats.Mean,
                StandardErrorNs: stats.StandardError,
                StandardDeviationNs: stats.StandardDeviation,
                AllocatedBytes: allocated,
                Gen0Collections: gen0,
                TotalOperations: totalOps);

            methods[report.BenchmarkCase.DisplayInfo] = metrics;
        }

        if (methods.Count == 0)
            return null;

        var machineInfo = summary.HostEnvironmentInfo.ToFormattedString().ToArray();
        var runtime = summary.HostEnvironmentInfo.RuntimeVersion;

        return new BenchmarkBaseline(
            SchemaVersion: BenchmarkBaseline.CurrentSchemaVersion,
            BenchmarkClass: classFullName,
            SavedAtUtc: DateTime.UtcNow,
            MachineInfo: machineInfo,
            Runtime: runtime,
            Methods: methods);
    }
}
