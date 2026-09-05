using Avalonia.Headless.XUnit;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.OgkrImpl;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenCheckerListViewer;

public sealed class CheckerRuleRegistrationTests
{
    private static readonly string[] ExpectedRuleNames =
    [
        "ColorIdCheckRule",
        "ColorfulLaneBrightnessCheckRule",
        "CommonObjectOverlapCheckRule",
        "CommonObjectTimelineNotAlignedCheckRule",
        "ConflictRecordIdLanesCheckRule",
        "DockableObjectWrongLocationCheckRule",
        "EnemySetCheckRule",
        "HeaderConstCheckRule",
        "InvalidConnectablePathCheckRule",
        "LaneBlockOnMultipleWallsCheckRule",
        "MissingHoldEndObjectCheckRule",
        "MissingRefObjectCheckRule",
        "NotInterpolatedCurveCheckRule",
        "SoflanCheckRule",
        "SoflanConflictCheckRule",
        "WallConflictCheckRule"
    ];

    private static readonly string[] ExpectedOngekiRuleNames =
    [
        "ColorIdCheckRule",
        "ColorfulLaneBrightnessCheckRule",
        "HeaderConstCheckRule",
        "NotInterpolatedCurveCheckRule"
    ];

    [AvaloniaFact]
    public void ContainerRegistersAllCheckerRulesAndRunsARegisteredRule()
    {
        var rules = IoC.GetAll<IFumenCheckRule>().ToArray();
        var ongekiRules = IoC.GetAll<IOngekiFumenCheckRule>().ToArray();

        Assert.Equal(
            ExpectedRuleNames.OrderBy(name => name, StringComparer.Ordinal),
            rules.Select(rule => rule.GetType().Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(
            ExpectedOngekiRuleNames.OrderBy(name => name, StringComparer.Ordinal),
            ongekiRules.Select(rule => rule.GetType().Name).OrderBy(name => name, StringComparer.Ordinal));

        var enemySetRule = Assert.Single(rules, rule => rule.GetType().Name == "EnemySetCheckRule");
        var results = enemySetRule.CheckRule(new OngekiFumen(), null!).ToArray();

        Assert.Contains(results, result => result.RuleName == "MissingBossEnemySet");
    }

    [AvaloniaFact]
    public void ConflictRecordIdRuleReportsDuplicateLaneGroup()
    {
        var fumen = new OngekiFumen();
        fumen.AddObject(new LaneLeftStart { RecordId = 42, TGrid = new TGrid(10), XGrid = new XGrid(0) });
        fumen.AddObject(new LaneRightStart { RecordId = 42, TGrid = new TGrid(20), XGrid = new XGrid(1) });

        var rule = Assert.Single(IoC.GetAll<IFumenCheckRule>(),
            candidate => candidate.GetType().Name == "ConflictRecordIdLanesCheckRule");
        var result = Assert.Single(rule.CheckRule(fumen, null!));

        Assert.Equal("ConflictRecordIdLanes", result.RuleName);
        Assert.Equal(RuleSeverity.Error, result.Severity);
        Assert.Contains("42", result.Description, StringComparison.Ordinal);
        Assert.NotNull(result.NavigateBehavior);
    }
}
