using BenchmarkDotNet.Reports;

namespace OngekiFumenEditor.Benchmark.Baselines;

/// <summary>
/// 所有 benchmark 跑完后,先询问用户是否展示基线对比(展示则按类逐个 Load 上次基线并渲染表格),
/// 再询问是否将当前结果作为新基线覆写保存。plan.md 第 5 条要求。
/// </summary>
public static class BaselineSavePrompt
{
    public static void HandleSummaries(IEnumerable<Summary> summaries)
    {
        var pending = new List<BenchmarkBaseline>();
        foreach (var summary in summaries)
        {
            if (summary is null || summary.HasCriticalValidationErrors || summary.Reports.Length == 0)
                continue;

            var current = BaselineFactory.FromSummary(summary);
            if (current is null)
                continue;

            pending.Add(current);
        }

        if (pending.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("[Baseline] 无可用 benchmark summary,跳过对比与保存。");
            return;
        }

        // stdin 被重定向(CI / 管道)时,默认两个询问全部跳过,保持无人值守安全。
        if (Console.IsInputRedirected)
        {
            Console.WriteLine();
            Console.WriteLine("[Baseline] stdin 已被重定向,跳过对比展示与基线保存。");
            return;
        }

        if (AskYesNo($"是否展示基线对比? ({pending.Count} 个 benchmark 类) [y/N]: "))
        {
            var useColor = ComparisonReporter.ShouldUseColor();
            foreach (var current in pending)
            {
                var previous = BaselineStore.Load(current.BenchmarkClass);
                var rows = ComparisonReporter.Compare(previous, current);
                ComparisonReporter.Render(current.BenchmarkClass, rows, Console.Out, useColor);
            }
        }
        else
        {
            Console.WriteLine("[Baseline] 已跳过对比展示。");
        }

        if (!AskYesNo($"是否将当前结果保存为新基线(覆写上次保存)? ({pending.Count} 个文件) [y/N]: "))
        {
            Console.WriteLine("[Baseline] 已跳过保存。");
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

    private static bool AskYesNo(string prompt)
    {
        Console.WriteLine();
        Console.Write(prompt);
        var line = Console.ReadLine();
        if (line is null)
            return false;
        var trimmed = line.Trim();
        return trimmed.Equals("y", StringComparison.OrdinalIgnoreCase)
               || trimmed.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }
}
