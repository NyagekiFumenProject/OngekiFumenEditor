using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using OngekiFumenEditor.Benchmark.Baselines;

namespace OngekiFumenEditor.Benchmark;

internal static class Program
{
    // 主项目是 WPF + Fody Costura,BenchmarkDotNet 默认工具链会自动生成 wrapper csproj
    // 并触发 WPF MarkupCompile 重跑,代价高且经常报错。改用 InProcessEmit 在当前已构建好
    // 的进程内 emit IL 跑 benchmark,跳过 wrapper 重建。
    private static readonly IToolchain InProcessToolchain =
        new InProcessEmitToolchain(TimeSpan.FromHours(2), logOutput: false);

    [STAThread]
    private static int Main(string[] args)
    {
        var (jobPreset, remainingArgs) = ParseCliArgs(args);

        var job = jobPreset.WithToolchain(InProcessToolchain);
        var config = DefaultConfig.Instance
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddJob(job);

        var benchmarkTypes = DiscoverBenchmarkTypes();
        if (benchmarkTypes.Count == 0)
        {
            Console.WriteLine("没有发现任何 Benchmark 类。");
            return 1;
        }

        // plan.md 第 4 条:程序需要询问用户测量哪个类。
        // CLI 透传参数(如 --filter)走 BenchmarkSwitcher 路径,跳过交互菜单。
        var hasSwitcherArgs = remainingArgs.Any(a =>
            a.StartsWith("--filter", StringComparison.OrdinalIgnoreCase)
            || a.Equals("--list", StringComparison.OrdinalIgnoreCase)
            || a.Equals("--help", StringComparison.OrdinalIgnoreCase));

        Summary[] summaries;
        if (hasSwitcherArgs)
        {
            var switcherSummaries = BenchmarkSwitcher
                .FromTypes(benchmarkTypes.ToArray())
                .Run(remainingArgs, config);
            summaries = switcherSummaries?.ToArray() ?? Array.Empty<Summary>();
        }
        else
        {
            var selected = PromptUserForBenchmarkSelection(benchmarkTypes);
            if (selected.Count == 0)
            {
                Console.WriteLine("未选中任何 Benchmark 类,退出。");
                return 0;
            }

            summaries = BenchmarkRunner.Run(selected.ToArray(), config);
        }

        // plan.md 第 5 条:跑完后对每个类对比上次基线,并询问是否覆写保存。
        try
        {
            BaselineSavePrompt.HandleSummaries(summaries);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Baseline] Post-run handler failed: {ex}");
        }

        return 0;
    }

    private static List<Type> DiscoverBenchmarkTypes()
    {
        var assembly = typeof(Program).Assembly;
        return assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true }
                        && t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                            .Any(m => m.GetCustomAttribute<BenchmarkAttribute>() is not null))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static List<Type> PromptUserForBenchmarkSelection(IReadOnlyList<Type> all)
    {
        Console.WriteLine();
        Console.WriteLine("请选择要测量的 Benchmark 类:");
        Console.WriteLine("  [0] 全部");
        for (var i = 0; i < all.Count; i++)
            Console.WriteLine($"  [{i + 1}] {all[i].Name}");

        Console.Write("输入编号(多个用逗号分隔,直接回车 = 全部): ");
        var line = Console.IsInputRedirected ? null : Console.ReadLine();
        if (string.IsNullOrWhiteSpace(line))
        {
            Console.WriteLine($"[Selection] 跑全部 {all.Count} 个 Benchmark 类。");
            return all.ToList();
        }

        var tokens = line.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries
                                                          | StringSplitOptions.TrimEntries);
        var picked = new List<Type>();
        var seenAll = false;
        foreach (var token in tokens)
        {
            if (!int.TryParse(token, out var idx))
            {
                Console.WriteLine($"[Selection] 忽略无法识别的输入: '{token}'");
                continue;
            }

            if (idx == 0)
            {
                seenAll = true;
                continue;
            }

            if (idx < 1 || idx > all.Count)
            {
                Console.WriteLine($"[Selection] 编号 {idx} 超出范围 1..{all.Count},忽略。");
                continue;
            }

            var type = all[idx - 1];
            if (!picked.Contains(type))
                picked.Add(type);
        }

        if (seenAll)
            return all.ToList();
        return picked;
    }

    private static (Job Job, string[] Remaining) ParseCliArgs(string[] args)
    {
        var preset = Job.Default;
        var remaining = new List<string>(args.Length);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if ((arg.Equals("--job", StringComparison.OrdinalIgnoreCase)
                    || arg.Equals("-j", StringComparison.OrdinalIgnoreCase))
                && i + 1 < args.Length)
            {
                preset = ResolveJobPreset(args[i + 1]) ?? preset;
                i++;
                continue;
            }

            if (arg.StartsWith("--job=", StringComparison.OrdinalIgnoreCase))
            {
                preset = ResolveJobPreset(arg["--job=".Length..]) ?? preset;
                continue;
            }

            remaining.Add(arg);
        }

        return (preset, remaining.ToArray());
    }

    private static Job? ResolveJobPreset(string name) => name.ToLowerInvariant() switch
    {
        "dry" => Job.Dry,
        "short" => Job.ShortRun,
        "medium" => Job.MediumRun,
        "long" => Job.LongRun,
        "verylong" => Job.VeryLongRun,
        "default" => Job.Default,
        _ => null
    };
}
