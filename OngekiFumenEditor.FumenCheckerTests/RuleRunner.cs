using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base;
using OngekiFumenEditor.Parser;
using System.IO;
using System.Text;

namespace OngekiFumenEditor.FumenCheckerTests;

internal sealed class RuleRunner
{
    private readonly IFumenParserManager parserManager;
    private readonly IFumenCheckRule[] rules;
    private readonly IFumenCheckContext context = new NullCheckContext();

    public RuleRunner(IFumenParserManager parserManager, IFumenCheckRule[] rules)
    {
        this.parserManager = parserManager;
        this.rules = rules;
    }

    public void Run(CheckCase testCase, CheckReporter reporter)
    {
        reporter.Section(testCase.Name);

        if (!TryParse(testCase.Chart, out var fumen, out var parseError, reporter))
        {
            reporter.Check($"{testCase.Name}: 谱面可加载", false, parseError);
            return;
        }

        reporter.Check($"{testCase.Name}: 谱面可加载", true);

        var findings = RunRules(fumen, reporter, testCase.Name);

        if (testCase.ExpectNoFindings)
        {
            reporter.Check(
                $"{testCase.Name}: 无任何检查结果",
                findings.Count == 0,
                findings.Count == 0 ? null : Describe(findings));
        }

        foreach (var expected in testCase.Expected)
        {
            var actual = findings.Where(x => x.RuleName == expected.RuleName).ToArray();
            reporter.Check(
                $"{testCase.Name}: {expected.RuleName} 命中 {expected.Count} 条",
                actual.Length == expected.Count,
                actual.Length == expected.Count ? actual.FirstOrDefault()?.Description : $"got {actual.Length}: {Describe(actual)}");

            if (actual.Length > 0)
            {
                reporter.Check(
                    $"{testCase.Name}: {expected.RuleName} 级别为 {expected.Severity}",
                    actual.All(x => x.Severity == expected.Severity),
                    string.Join(",", actual.Select(x => x.Severity).Distinct()));
            }
        }

        foreach (var forbidden in testCase.Forbidden)
        {
            var actual = findings.Where(x => x.RuleName == forbidden).ToArray();
            reporter.Check(
                $"{testCase.Name}: 不应触发 {forbidden}",
                actual.Length == 0,
                actual.Length == 0 ? null : $"got {actual.Length}: {Describe(actual)}");
        }
    }

    /// <summary>把全部规则跑一遍 Benchmark 样例谱面：不允许任何规则抛异常，新增规则不允许误报。</summary>
    public void Sweep(string[] chartPaths, CheckReporter reporter, string[] newRuleNames, (string Chart, string RuleName)[] knownFindings)
    {
        reporter.Section("Benchmark 样例谱面 sweep");

        if (chartPaths.Length == 0)
        {
            Console.WriteLine("  [WARN] 未找到 OngekiFumenEditor.Benchmark/Data/FumenSamples（需要在仓库内运行），跳过。");
            return;
        }

        var parseFailures = new List<string>();
        var unexpectedHits = new List<string>();
        var knownHits = new List<string>();

        foreach (var path in chartPaths)
        {
            var name = Path.GetFileName(path);
            if (!TryParse(File.ReadAllText(path), out var fumen, out var parseError, reporter))
            {
                parseFailures.Add($"{name}: {parseError}");
                continue;
            }

            foreach (var finding in RunRules(fumen, reporter, name).Where(x => newRuleNames.Contains(x.RuleName)))
            {
                if (knownFindings.Contains((name, finding.RuleName)))
                    knownHits.Add($"{name} :: {finding.RuleName}");
                else
                    unexpectedHits.Add($"{name} :: {finding.RuleName} :: {finding.Description}");
            }
        }

        reporter.Check(
            $"样例谱面全部可加载（{chartPaths.Length} 张）",
            parseFailures.Count == 0,
            parseFailures.Count == 0 ? null : string.Join(" | ", parseFailures.Take(5)));

        reporter.Check(
            "新增规则在样例谱面上无误报",
            unexpectedHits.Count == 0,
            unexpectedHits.Count == 0
                ? (knownHits.Count == 0 ? null : $"已核对命中: {string.Join(", ", knownHits)}")
                : string.Join(" | ", unexpectedHits.Take(5)));
    }

    private List<Finding> RunRules(OngekiFumen fumen, CheckReporter reporter, string contextName)
    {
        var findings = new List<Finding>();

        foreach (var rule in rules)
        {
            if (reporter.Verbose)
                Console.WriteLine($"      [rule] {rule.GetType().Name}");

            try
            {
                foreach (var result in rule.CheckRule(fumen, context))
                {
                    findings.Add(new Finding(
                        result.RuleName,
                        result.Severity.ToString(),
                        result.Description ?? string.Empty,
                        result.LocationDescription ?? string.Empty));
                }
            }
            catch (Exception ex)
            {
                reporter.Check($"{contextName}: 规则 {rule.GetType().Name} 不应抛异常", false, $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        return findings;
    }

    internal bool TryParse(string chartText, out OngekiFumen fumen, out string error, CheckReporter? reporter = null)
    {
        try
        {
            if (reporter?.Verbose == true)
                Console.WriteLine("      [parse] resolving deserializer");

            var deserializer = parserManager.GetDeserializer(".ogkr")
                ?? throw new InvalidOperationException("no deserializer registered for .ogkr");

            if (reporter?.Verbose == true)
                Console.WriteLine($"      [parse] deserializing ({chartText.Length} chars)");

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(chartText), writable: false);
            fumen = deserializer.DeserializeAsync(stream).GetAwaiter().GetResult();
            error = string.Empty;

            if (reporter?.Verbose == true)
                Console.WriteLine($"      [parse] ok, objects={fumen.GetAllDisplayableObjects().Count()}, issues={fumen.ParseIssues.Count}");

            return true;
        }
        catch (Exception ex)
        {
            fumen = null!;
            error = $"{ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    private static string Describe(IEnumerable<Finding> findings)
        => string.Join(" | ", findings.Select(x => $"{x.RuleName}: {x.Description}"));
}
