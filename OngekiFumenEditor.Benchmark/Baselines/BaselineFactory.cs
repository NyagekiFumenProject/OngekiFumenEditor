using BenchmarkDotNet.Reports;

namespace OngekiFumenEditor.Benchmark.Baselines;

public static class BaselineFactory
{
    public static BenchmarkBaseline? FromSummary(Summary summary)
    {
        if (summary.HasCriticalValidationErrors || summary.Reports.Length == 0)
            return null;

        var firstReport = summary.Reports[0];
        var classFullName = firstReport.BenchmarkCase.Descriptor.Type.FullName
                            ?? firstReport.BenchmarkCase.Descriptor.Type.Name;

        var methods = new Dictionary<string, MethodMetric>();
        foreach (var report in summary.Reports)
        {
            if (!report.Success || report.ResultStatistics is null)
                continue;

            var stats = report.ResultStatistics;
            var allocated = report.GcStats.GetBytesAllocatedPerOperation(report.BenchmarkCase);
            var gen0 = report.GcStats.GetCollectionsCount(0);
            var totalOps = report.GcStats.TotalOperations;

            methods[report.BenchmarkCase.DisplayInfo] = new MethodMetric(
                MeanNs: stats.Mean,
                StandardErrorNs: stats.StandardError,
                StandardDeviationNs: stats.StandardDeviation,
                AllocatedBytes: allocated,
                Gen0Collections: gen0,
                TotalOperations: totalOps);
        }

        if (methods.Count == 0)
            return null;

        return new BenchmarkBaseline(
            SchemaVersion: BenchmarkBaseline.CurrentSchemaVersion,
            BenchmarkClass: classFullName,
            SavedAtUtc: DateTime.UtcNow,
            MachineInfo: summary.HostEnvironmentInfo.ToFormattedString().ToArray(),
            Runtime: summary.HostEnvironmentInfo.RuntimeVersion,
            Methods: methods);
    }
}
