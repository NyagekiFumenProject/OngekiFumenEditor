using Avalonia.Headless.XUnit;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.OgkrImpl;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenCheckerListViewer;

public sealed class CheckerRuleRegistrationTests
{
    private static readonly string[] ExpectedRuleNames =
    [
        "BulletNullPalleteCheckRule",
        "ColorIdCheckRule",
        "ColorfulLaneBrightnessCheckRule",
        "CommonObjectOverlapCheckRule",
        "CommonObjectTimelineNotAlignedCheckRule",
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
        "BulletNullPalleteCheckRule",
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
}
