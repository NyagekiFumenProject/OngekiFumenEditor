using System.IO;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains;
using OngekiFumenEditor.Benchmark.Baselines;
using OngekiFumenEditor.Benchmark.Infrastructure;

namespace OngekiFumenEditor.Benchmark;

internal static class Program
{
    // 使用基于 BDN 默认 CsProjCoreToolchain 的自定义 Toolchain (HintPathRewriteToolchain):
    // 复用默认 Builder/Executor,只在 IGenerator 层后处理 wrapper csproj,把 ProjectReference
    // 链替换为指向已编译主 Benchmark bin 目录的 Reference HintPath。这样 wrapper 不再重建
    // Caliburn.Micro.Core / OngekiFumenEditor 等 dll,从根上规避之前撞过的 CSC CS2012 文件锁;
    // 同时保留了子进程隔离的测量精度,不必走 InProcessEmit。
    //
    // 前提: 主 Benchmark dll 必须先 build 一次,wrapper 才有 dll 可 link。
    // `dotnet run` 默认会先 build,无需手动。

    [STAThread]
    private static int Main(string[] args)
    {
        var (jobPreset, remainingArgs) = ParseCliArgs(args);

        var hostBin = Path.GetDirectoryName(typeof(Program).Assembly.Location)
            ?? throw new InvalidOperationException("Cannot locate host benchmark bin directory.");
        var toolchain = HintPathRewriteToolchain.Create("net10.0-windows", hostBin);

        var job = jobPreset.WithToolchain(toolchain);
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
