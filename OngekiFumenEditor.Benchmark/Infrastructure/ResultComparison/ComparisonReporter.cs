using System.Globalization;
using System.IO;

namespace OngekiFumenEditor.Benchmark.Infrastructure.ResultComparison;

public static class ComparisonReporter
{
    private const string AnsiRed = "[31m";
    private const string AnsiGreen = "[32m";
    private const string AnsiYellow = "[33m";
    private const string AnsiGray = "[90m";
    private const string AnsiReset = "[0m";

    public static List<ComparisonResult> Compare(
        BenchmarkBaseline? previous,
        BenchmarkBaseline current,
        BaselineOptions options)
    {
        var results = new List<ComparisonResult>();
        var prevMethods = previous?.Methods ?? new Dictionary<string, MethodMetrics>();
        var allKeys = new HashSet<string>(prevMethods.Keys);
        allKeys.UnionWith(current.Methods.Keys);

        foreach (var key in allKeys.OrderBy(k => k, StringComparer.Ordinal))
        {
            prevMethods.TryGetValue(key, out var prev);
            current.Methods.TryGetValue(key, out var curr);

            var (meanStatus, meanDelta) = ClassifyMean(prev, curr, options.MeanThresholdPercent);
            var (allocStatus, allocDelta) = ClassifyAlloc(prev, curr, options.AllocThresholdPercent);

            results.Add(new ComparisonResult(
                Method: key,
                Previous: prev,
                Current: curr,
                MeanStatus: meanStatus,
                AllocStatus: allocStatus,
                MeanDeltaPercent: meanDelta,
                AllocDeltaPercent: allocDelta));
        }

        return results;
    }

    public static void Render(
        string benchmarkClass,
        IReadOnlyList<ComparisonResult> comparisons,
        TextWriter writer,
        bool useColor)
    {
        writer.WriteLine();
        writer.WriteLine($"=== Baseline comparison: {benchmarkClass} ===");

        if (comparisons.Count == 0)
        {
            writer.WriteLine("  (no methods to compare)");
            return;
        }

        const int methodWidth = 50;
        const int valueWidth = 13;
        const int deltaWidth = 10;
        const int statusWidth = 11;

        writer.WriteLine(
            $"{"Method".PadRight(methodWidth)}  " +
            $"{"PrevMean".PadLeft(valueWidth)} {"CurrMean".PadLeft(valueWidth)} {"Mean Δ%".PadLeft(deltaWidth)}  " +
            $"{"PrevAlloc".PadLeft(valueWidth)} {"CurrAlloc".PadLeft(valueWidth)} {"Alloc Δ%".PadLeft(deltaWidth)}  " +
            $"{"Status".PadLeft(statusWidth)}");

        writer.WriteLine(new string('-', methodWidth + 2 + (valueWidth + 1) * 4 + (deltaWidth + 1) * 2 + 1 + statusWidth));

        foreach (var c in comparisons)
        {
            var method = Truncate(c.Method, methodWidth);
            var prevMean = c.Previous is null ? "—" : FormatNs(c.Previous.MeanNs);
            var currMean = c.Current is null ? "—" : FormatNs(c.Current.MeanNs);
            var meanDelta = FormatDelta(c.MeanDeltaPercent);

            var prevAlloc = FormatBytes(c.Previous?.AllocatedBytes);
            var currAlloc = FormatBytes(c.Current?.AllocatedBytes);
            var allocDelta = FormatDelta(c.AllocDeltaPercent);

            var overallStatus = CombineStatus(c.MeanStatus, c.AllocStatus);
            var statusText = overallStatus.ToString().ToUpperInvariant();

            var line =
                $"{method.PadRight(methodWidth)}  " +
                $"{prevMean.PadLeft(valueWidth)} {currMean.PadLeft(valueWidth)} {meanDelta.PadLeft(deltaWidth)}  " +
                $"{prevAlloc.PadLeft(valueWidth)} {currAlloc.PadLeft(valueWidth)} {allocDelta.PadLeft(deltaWidth)}  " +
                $"{statusText.PadLeft(statusWidth)}";

            if (useColor)
                writer.WriteLine(Colorize(line, overallStatus));
            else
                writer.WriteLine(line);
        }
    }

    public static bool ShouldUseColor()
    {
        if (Console.IsOutputRedirected)
            return false;
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")))
            return false;
        return true;
    }

    private static (ComparisonStatus Status, double? Delta) ClassifyMean(
        MethodMetrics? prev, MethodMetrics? curr, double thresholdPercent)
    {
        if (curr is null && prev is not null)
            return (ComparisonStatus.Gone, null);
        if (prev is null && curr is not null)
            return (ComparisonStatus.New, null);
        if (prev is null || curr is null || prev.MeanNs <= 0)
            return (ComparisonStatus.Unknown, null);

        var delta = (curr.MeanNs - prev.MeanNs) / prev.MeanNs * 100.0;
        if (delta > thresholdPercent)
            return (ComparisonStatus.Regression, delta);
        if (delta < -thresholdPercent)
            return (ComparisonStatus.Improved, delta);
        return (ComparisonStatus.Ok, delta);
    }

    private static (ComparisonStatus Status, double? Delta) ClassifyAlloc(
        MethodMetrics? prev, MethodMetrics? curr, double thresholdPercent)
    {
        if (curr is null && prev is not null)
            return (ComparisonStatus.Gone, null);
        if (prev is null && curr is not null)
            return (ComparisonStatus.New, null);
        if (prev is null || curr is null)
            return (ComparisonStatus.Unknown, null);

        var prevAlloc = prev.AllocatedBytes;
        var currAlloc = curr.AllocatedBytes;
        if (prevAlloc is null || currAlloc is null)
            return (ComparisonStatus.Unknown, null);
        if (prevAlloc.Value == 0)
            return currAlloc.Value == 0 ? (ComparisonStatus.Ok, 0.0) : (ComparisonStatus.Regression, null);

        var delta = (currAlloc.Value - prevAlloc.Value) / (double)prevAlloc.Value * 100.0;
        if (delta > thresholdPercent)
            return (ComparisonStatus.Regression, delta);
        if (delta < -thresholdPercent)
            return (ComparisonStatus.Improved, delta);
        return (ComparisonStatus.Ok, delta);
    }

    private static ComparisonStatus CombineStatus(ComparisonStatus mean, ComparisonStatus alloc)
    {
        if (mean == ComparisonStatus.New || alloc == ComparisonStatus.New) return ComparisonStatus.New;
        if (mean == ComparisonStatus.Gone || alloc == ComparisonStatus.Gone) return ComparisonStatus.Gone;
        if (mean == ComparisonStatus.Regression || alloc == ComparisonStatus.Regression) return ComparisonStatus.Regression;
        if (mean == ComparisonStatus.Improved || alloc == ComparisonStatus.Improved) return ComparisonStatus.Improved;
        if (mean == ComparisonStatus.Unknown && alloc == ComparisonStatus.Unknown) return ComparisonStatus.Unknown;
        return ComparisonStatus.Ok;
    }

    private static string Colorize(string text, ComparisonStatus status) => status switch
    {
        ComparisonStatus.Regression => $"{AnsiRed}{text}{AnsiReset}",
        ComparisonStatus.Improved => $"{AnsiGreen}{text}{AnsiReset}",
        ComparisonStatus.New => $"{AnsiYellow}{text}{AnsiReset}",
        ComparisonStatus.Gone => $"{AnsiGray}{text}{AnsiReset}",
        _ => text
    };

    private static string FormatNs(double ns)
    {
        if (double.IsNaN(ns) || double.IsInfinity(ns))
            return "n/a";
        if (ns < 1_000) return $"{ns:F1} ns";
        if (ns < 1_000_000) return $"{ns / 1_000:F3} μs";
        if (ns < 1_000_000_000) return $"{ns / 1_000_000:F3} ms";
        return $"{ns / 1_000_000_000:F3} s";
    }

    private static string FormatBytes(long? bytes)
    {
        if (bytes is null) return "—";
        var b = bytes.Value;
        if (b < 1024) return $"{b} B";
        if (b < 1024 * 1024) return $"{b / 1024.0:F2} KB";
        return $"{b / (1024.0 * 1024.0):F2} MB";
    }

    private static string FormatDelta(double? delta)
    {
        if (delta is null) return "n/a";
        var sign = delta.Value > 0 ? "+" : "";
        return $"{sign}{delta.Value.ToString("F2", CultureInfo.InvariantCulture)}%";
    }

    private static string Truncate(string s, int max)
    {
        if (s.Length <= max) return s;
        return s.Substring(0, max - 1) + "…";
    }
}
