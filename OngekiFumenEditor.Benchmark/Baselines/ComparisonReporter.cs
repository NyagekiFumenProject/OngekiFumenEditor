using System.Globalization;
using System.IO;

namespace OngekiFumenEditor.Benchmark.Baselines;

public sealed record ComparisonRow(
    string Method,
    MethodMetric? Previous,
    MethodMetric? Current,
    double? MeanDeltaPercent,
    double? AllocDeltaPercent);

/// <summary>
/// 把 BenchmarkDotNet 当前结果与上次保存的 baseline 做差异比对,渲染表格输出。
/// 阈值: Mean ±5%, Allocated ±1% 用作 Δ% 染色判定(红=回归 / 绿=改善)。
/// </summary>
public static class ComparisonReporter
{
    private const double MeanThresholdPercent = 5.0;
    private const double AllocThresholdPercent = 1.0;

    private const string AnsiRed = "\x1b[31m";
    private const string AnsiGreen = "\x1b[32m";
    private const string AnsiReset = "\x1b[0m";

    public static List<ComparisonRow> Compare(BenchmarkBaseline? previous, BenchmarkBaseline current)
    {
        var rows = new List<ComparisonRow>();
        var prevMethods = previous?.Methods ?? new Dictionary<string, MethodMetric>();
        var allKeys = new HashSet<string>(prevMethods.Keys);
        allKeys.UnionWith(current.Methods.Keys);

        foreach (var key in allKeys.OrderBy(k => k, StringComparer.Ordinal))
        {
            prevMethods.TryGetValue(key, out var prev);
            current.Methods.TryGetValue(key, out var curr);

            rows.Add(new ComparisonRow(
                Method: key,
                Previous: prev,
                Current: curr,
                MeanDeltaPercent: DeltaPercent(prev?.MeanNs, curr?.MeanNs),
                AllocDeltaPercent: DeltaPercent(prev?.AllocatedBytes, curr?.AllocatedBytes)));
        }

        return rows;
    }

    public static void Render(
        string benchmarkClass,
        IReadOnlyList<ComparisonRow> rows,
        TextWriter writer,
        bool useColor)
    {
        writer.WriteLine();
        writer.WriteLine($"=== Baseline comparison: {benchmarkClass} ===");

        if (rows.Count == 0)
        {
            writer.WriteLine("  (no methods to compare)");
            return;
        }

        const int methodWidth = 50;
        const int timeWidth = 11;
        const int allocWidth = 11;
        const int countWidth = 9;
        const int deltaWidth = 9;

        var headerCols = new (string Label, int Width)[]
        {
            ("Method", methodWidth),
            ("PrevMean", timeWidth),
            ("CurrMean", timeWidth),
            ("Mean Δ%", deltaWidth),
            ("PrevStdDev", timeWidth),
            ("CurrStdDev", timeWidth),
            ("PrevStdErr", timeWidth),
            ("CurrStdErr", timeWidth),
            ("PrevAlloc", allocWidth),
            ("CurrAlloc", allocWidth),
            ("Alloc Δ%", deltaWidth),
            ("PrevGen0", countWidth),
            ("CurrGen0", countWidth),
            ("PrevOps", countWidth),
            ("CurrOps", countWidth)
        };

        var header = string.Join("  ", headerCols.Select((c, i) =>
            i == 0 ? c.Label.PadRight(c.Width) : c.Label.PadLeft(c.Width)));
        writer.WriteLine(header);
        writer.WriteLine(new string('-', header.Length));

        foreach (var r in rows)
        {
            var method = Truncate(r.Method, methodWidth).PadRight(methodWidth);
            var prevMean = FormatNs(r.Previous?.MeanNs).PadLeft(timeWidth);
            var currMean = FormatNs(r.Current?.MeanNs).PadLeft(timeWidth);
            var meanDelta = ColorizeDelta(r.MeanDeltaPercent, MeanThresholdPercent, deltaWidth, useColor);
            var prevStdDev = FormatNs(r.Previous?.StandardDeviationNs).PadLeft(timeWidth);
            var currStdDev = FormatNs(r.Current?.StandardDeviationNs).PadLeft(timeWidth);
            var prevStdErr = FormatNs(r.Previous?.StandardErrorNs).PadLeft(timeWidth);
            var currStdErr = FormatNs(r.Current?.StandardErrorNs).PadLeft(timeWidth);
            var prevAlloc = FormatBytes(r.Previous?.AllocatedBytes).PadLeft(allocWidth);
            var currAlloc = FormatBytes(r.Current?.AllocatedBytes).PadLeft(allocWidth);
            var allocDelta = ColorizeDelta(r.AllocDeltaPercent, AllocThresholdPercent, deltaWidth, useColor);
            var prevGen0 = FormatLong(r.Previous?.Gen0Collections).PadLeft(countWidth);
            var currGen0 = FormatLong(r.Current?.Gen0Collections).PadLeft(countWidth);
            var prevOps = FormatLong(r.Previous?.TotalOperations).PadLeft(countWidth);
            var currOps = FormatLong(r.Current?.TotalOperations).PadLeft(countWidth);

            writer.WriteLine(string.Join("  ", new[]
            {
                method, prevMean, currMean, meanDelta,
                prevStdDev, currStdDev, prevStdErr, currStdErr,
                prevAlloc, currAlloc, allocDelta,
                prevGen0, currGen0, prevOps, currOps
            }));
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

    private static string ColorizeDelta(double? delta, double thresholdPercent, int width, bool useColor)
    {
        var text = FormatDelta(delta).PadLeft(width);
        if (!useColor || delta is null)
            return text;
        if (delta.Value > thresholdPercent)
            return $"{AnsiRed}{text}{AnsiReset}";
        if (delta.Value < -thresholdPercent)
            return $"{AnsiGreen}{text}{AnsiReset}";
        return text;
    }

    private static double? DeltaPercent(double? prev, double? curr)
    {
        if (prev is null || curr is null) return null;
        if (prev.Value <= 0) return null;
        return (curr.Value - prev.Value) / prev.Value * 100.0;
    }

    private static double? DeltaPercent(long? prev, long? curr)
    {
        if (prev is null || curr is null) return null;
        if (prev.Value == 0) return curr.Value == 0 ? 0.0 : null;
        return (curr.Value - prev.Value) / (double)prev.Value * 100.0;
    }

    private static string FormatNs(double? ns)
    {
        if (ns is null) return "—";
        var v = ns.Value;
        if (double.IsNaN(v) || double.IsInfinity(v)) return "n/a";
        if (v < 1_000) return $"{v:F1} ns";
        if (v < 1_000_000) return $"{v / 1_000:F3} μs";
        if (v < 1_000_000_000) return $"{v / 1_000_000:F3} ms";
        return $"{v / 1_000_000_000:F3} s";
    }

    private static string FormatBytes(long? bytes)
    {
        if (bytes is null) return "—";
        var b = bytes.Value;
        if (b < 1024) return $"{b} B";
        if (b < 1024 * 1024) return $"{b / 1024.0:F2} KB";
        return $"{b / (1024.0 * 1024.0):F2} MB";
    }

    private static string FormatLong(long? v)
        => v is null ? "—" : v.Value.ToString("N0", CultureInfo.InvariantCulture);

    private static string FormatDelta(double? delta)
    {
        if (delta is null) return "—";
        var sign = delta.Value > 0 ? "+" : "";
        return $"{sign}{delta.Value.ToString("F2", CultureInfo.InvariantCulture)}%";
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : string.Concat(s.AsSpan(0, max - 1), "…");
}
