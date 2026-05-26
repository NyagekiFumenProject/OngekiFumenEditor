using BenchmarkDotNet.Reports;

namespace OngekiFumenEditor.Benchmark.Baselines;

/// <summary>
/// 在所有 benchmark 跑完后,对每份 Summary 加载上一次保存的 baseline、
/// 渲染对比表格,并交互式询问用户是否将本次结果作为新基线覆写保存。
/// plan.md 第 5 条要求。
/// </summary>
public static class BaselineSavePrompt
{
    public static void HandleSummaries(IEnumerable<Summary> summaries)
    {
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
            var rows = ComparisonReporter.Compare(previous, current);
            ComparisonReporter.Render(current.BenchmarkClass, rows, Console.Out, useColor);
            pending.Add(current);
        }

        if (pending.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("[Baseline] No comparable benchmark summaries; nothing to save.");
            return;
        }

        if (!AskShouldSave(pending.Count))
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

    private static bool AskShouldSave(int pendingCount)
    {
        Console.WriteLine();

        // 重定向输入(CI、管道)默认不存盘,避免无人值守时误覆写。
        if (Console.IsInputRedirected)
        {
            Console.WriteLine("[Baseline] stdin is redirected; auto-skipping save prompt.");
            return false;
        }

        Console.Write($"Save current results as new baseline(s)? ({pendingCount} file(s)) [y/N]: ");
        var response = Console.ReadLine();
        if (response is null)
            return false;
        var trimmed = response.Trim();
        return trimmed.Equals("y", StringComparison.OrdinalIgnoreCase)
               || trimmed.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }
}
