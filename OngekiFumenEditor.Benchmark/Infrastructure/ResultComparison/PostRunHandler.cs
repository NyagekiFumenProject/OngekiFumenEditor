using BenchmarkDotNet.Reports;

namespace OngekiFumenEditor.Benchmark.Infrastructure.ResultComparison;

public static class PostRunHandler
{
    public static void HandleSummaries(IEnumerable<Summary> summaries, BaselineOptions options)
    {
        if (options.Disabled)
            return;

        var pending = new List<BenchmarkBaseline>();
        var useColor = ComparisonReporter.ShouldUseColor();

        foreach (var summary in summaries)
        {
            if (summary is null || summary.HasCriticalValidationErrors || summary.Reports.Length == 0)
                continue;

            var current = BaselineFactory.FromSummary(summary);
            if (current is null)
                continue;

            var previous = BaselineStore.Load(current.BenchmarkClass);
            var comparisons = ComparisonReporter.Compare(previous, current, options);
            ComparisonReporter.Render(current.BenchmarkClass, comparisons, Console.Out, useColor);

            pending.Add(current);
        }

        if (pending.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("[Baseline] No comparable benchmark summaries; nothing to save.");
            return;
        }

        var shouldSave = ResolveSaveDecision(pending.Count, options);
        if (!shouldSave)
        {
            Console.WriteLine("[Baseline] Skipped saving baselines.");
            return;
        }

        foreach (var baseline in pending)
        {
            try
            {
                BaselineStore.Save(baseline);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Baseline] Failed to save '{baseline.BenchmarkClass}': {ex.Message}");
            }
        }
    }

    private static bool ResolveSaveDecision(int pendingCount, BaselineOptions options)
    {
        Console.WriteLine();

        switch (options.Mode)
        {
            case BaselineMode.Save:
                Console.WriteLine($"[Baseline] Auto-saving {pendingCount} baseline file(s) (mode=save).");
                return true;

            case BaselineMode.Skip:
                Console.WriteLine("[Baseline] Save skipped (mode=skip).");
                return false;

            case BaselineMode.Prompt:
            default:
                if (Console.IsInputRedirected)
                {
                    Console.WriteLine("[Baseline] stdin is redirected; auto-skipping save. Pass --save-baseline or BENCHMARK_BASELINE_MODE=save to save anyway.");
                    return false;
                }

                Console.Write($"Save all current results as new baselines? ({pendingCount} file(s)) [y/N]: ");
                var response = Console.ReadLine();
                if (response is null)
                    return false;
                var trimmed = response.Trim();
                return trimmed.Equals("y", StringComparison.OrdinalIgnoreCase)
                       || trimmed.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
