using System.IO;
using System.Windows;
using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base;
using OngekiFumenEditor.Parser;

namespace OngekiFumenEditor.FumenCheckerTests;

/// <summary>
/// 谱面检查规则的常驻验收：
/// 1) 用内联 ogkr 文本驱动真实解析器与真实检查规则，逐条断言 8 条「游戏侧会崩溃」规则
///    （BulletPalleteDuplicateId / SoflanPatternMissingForArea / OrphanLaneRecord / ColorfulLaneRecordColumns /
///     BeamRecordColumns / HoldProgressJudgeLoop / BpmOutOfRange / MissingEnemySetWave）的触发与不触发；
/// 2) 把全部规则跑一遍 Benchmark 样例谱面，验证没有规则抛异常、新规则不误报。
///
/// usage: dotnet run --project OngekiFumenEditor.FumenCheckerTests -c Release
/// 退出码 1 表示有检查失败。
///
/// 本工程是测试类工程，**不要加进 `OngekiFumenEditor.sln`**（该解决方案只装生产工程，
/// 测试按 csproj 单独调用；同 Avalonia 侧的约定）。可选参数：
/// `--verbose` 逐规则打印进度，`--parse-only &lt;path&gt;` 只解析一张谱面用于诊断。
/// </summary>
internal static class Program
{
    /// <summary>装配的规则数量：15 条既有 + 8 条新增（WallConflictCheckRule 至今没有 [Export]，不计入）。</summary>
    private const int ExpectedRuleCount = 23;

    [STAThread]
    private static int Main(string[] args)
    {
        // 与 OngekiFumenEditor.CommandLine/Program.cs 一致：Gemini 引导器会递归扫描「当前工作目录」下
        // 的所有 dll 当 MEF 目录，必须先把 CWD 切到自己 bin，否则会扫到仓库里其它工程的产物。
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        Console.WriteLine($"[boot] base directory: {AppContext.BaseDirectory}");

        // 与 OngekiFumenEditor.Benchmark/Infrastructure/BenchmarkRuntime 相同的引导流程：
        // new App(false) → new AppBootstrapper(false) → IoC.Get<T>() 即可使用 MEF 服务。
        if (Application.Current is null)
            _ = new App(false);
        _ = new AppBootstrapper(false)
        {
            IsGUIMode = false,
        };

        // Caliburn 在 WPF 下会把属性变更通知 marshal 到 Dispatcher（XamlPlatformProvider → Dispatcher.Invoke）。
        // 本 harness 没有消息泵，装载一张谱面（OngekiFumen 构造函数里就会有属性通知）就会死锁，
        // 因此换成同步执行的 provider —— 这也是它不需要桌面会话的原因。
        PlatformProvider.Current = new InlinePlatformProvider();

        Console.WriteLine("[boot] editor services ready");

        var reporter = new CheckReporter(verbose: args.Contains("--verbose"));

        Console.WriteLine("[boot] resolving services");
        var parserManager = IoC.Get<IFumenParserManager>();
        var rules = IoC.GetAll<IFumenCheckRule>().ToArray();

        var runner = new RuleRunner(parserManager, rules);

        // 诊断模式：--parse-only <path> 只解析一张谱面并打印结果，用于定位解析阶段的挂死/异常。
        var parseOnlyIndex = Array.IndexOf(args, "--parse-only");
        if (parseOnlyIndex >= 0 && parseOnlyIndex + 1 < args.Length)
        {
            var chartPath = args[parseOnlyIndex + 1];
            var parsed = runner.TryParse(File.ReadAllText(chartPath), out var single, out var parseError, reporter);
            Console.WriteLine(parsed
                ? $"[parse-only] ok: objects={single.GetAllDisplayableObjects().Count()} issues={single.ParseIssues.Count}"
                : $"[parse-only] failed: {parseError}");
            return parsed ? 0 : 1;
        }

        reporter.Section("规则装配");
        reporter.Check("导出的检查规则数量", rules.Length == ExpectedRuleCount, $"expected {ExpectedRuleCount}, got {rules.Length}");

        foreach (var testCase in RuleChecks.Cases)
            runner.Run(testCase, reporter);

        runner.Sweep(SampleCorpus.FindCharts(), reporter, RuleChecks.NewRuleNames, RuleChecks.KnownSampleFindings);

        return reporter.PrintSummary();
    }
}
