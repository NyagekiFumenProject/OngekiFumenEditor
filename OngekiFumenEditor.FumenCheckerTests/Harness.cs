using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base;

namespace OngekiFumenEditor.FumenCheckerTests;

/// <summary>
/// 同步执行 OnUIThread 的 platform provider：harness 里没有 WPF 消息泵，
/// 默认（XamlPlatformProvider）的 Dispatcher marshaling 会让装载谱面直接死锁。
/// </summary>
internal sealed class InlinePlatformProvider : DefaultPlatformProvider
{
    public override bool InDesignMode => false;
}

internal sealed record Finding(string RuleName, string Severity, string Description, string Location);

internal sealed record Expectation(string RuleName, int Count, string Severity = "Error");

internal sealed class CheckCase
{
    public required string Name { get; init; }
    public required string Chart { get; init; }

    /// <summary>整张谱面不得有任何检查结果（golden 基线）。</summary>
    public bool ExpectNoFindings { get; init; }

    public Expectation[] Expected { get; init; } = Array.Empty<Expectation>();

    /// <summary>必须不出现的规则名（用于负向用例）。</summary>
    public string[] Forbidden { get; init; } = Array.Empty<string>();
}

/// <summary>规则只把 context 用于双击跳转，跑规则时不需要真实宿主视图。</summary>
internal sealed class NullCheckContext : IFumenCheckContext
{
    public void ScrollTo(TGrid tGrid) { }

    public void ScrollTo(OngekiTimelineObjectBase ongekiObject) { }

    public void NotifyObjectClicked(OngekiTimelineObjectBase ongekiObject) { }

    public void ShowFumenMetaInfo() { }
}

internal sealed class CheckReporter
{
    private int passed;
    private int failed;

    public CheckReporter(bool verbose = false) => Verbose = verbose;

    public bool Verbose { get; }

    public int Failed => failed;

    public void Section(string title)
        => Console.WriteLine($"\n--- {title} {new string('-', Math.Max(0, 56 - title.Length))}");

    public void Check(string name, bool ok, string? detail = null)
    {
        var suffix = string.IsNullOrEmpty(detail) ? string.Empty : $"  :: {detail}";
        if (ok)
        {
            passed++;
            Console.WriteLine($"  PASS  {name}{suffix}");
        }
        else
        {
            failed++;
            Console.WriteLine($"  FAIL  {name}{suffix}");
        }
    }

    public int PrintSummary()
    {
        Console.WriteLine($"\n{new string('=', 64)}");
        Console.WriteLine($"TOTAL: {passed}/{passed + failed} checks passed");
        if (failed > 0)
            Console.WriteLine($"FAILED: {failed} check(s)");
        Console.WriteLine(new string('=', 64));
        return failed == 0 ? 0 : 1;
    }
}
