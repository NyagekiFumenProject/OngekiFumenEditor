using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Utils;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Base.OngekiObjects;

public class HoldTickStepCalculatorTests
{
    private const int Base = HoldTickStepCalculator.DefaultStandardBeatLen;

    public HoldTickStepCalculatorTests()
    {
        // Log.LogError 走 IoC 单例，测试里显式注入一个不落盘的实例（与既有测试同一写法）。
        Log.Initialize(new Log([]));
    }

    public static TheoryData<double, double> NonAdvancingInputs() => new()
    {
        { 0d, 0d },
        { 0d, 240d },
        { 240d, 0d },
        { -240d, 240d },
        { 240d, -240d },
        { double.NaN, 240d },
        { 240d, double.NaN },
        { double.PositiveInfinity, 240d },
        { 240d, double.PositiveInfinity },
        { double.NegativeInfinity, 240d },
        { 240d, double.NegativeInfinity },
        { double.Epsilon, 240d },
        { 240d, double.Epsilon },
        { 1e-300, 1e300 },
        { 1e300, 1e-300 },
    };

    [Theory]
    [MemberData(nameof(NonAdvancingInputs))]
    public void Calculate_AlwaysReturnsAdvancingStep(double bpm, double progressJudgeBpm)
        => Assert.True(
            HoldTickStepCalculator.Calculate(bpm, progressJudgeBpm, Base) >= 1,
            $"bpm={bpm}, progressJudgeBpm={progressJudgeBpm}");

    [Theory]
    [InlineData(240d, 240d, 0)]
    [InlineData(120d, 240d, -1)]
    [InlineData(60d, 240d, -2)]
    [InlineData(480d, 240d, 1)]
    [InlineData(960d, 240d, 2)]
    public void Calculate_ScalesLikeTheLegacyClosedForm(double bpm, double progressJudgeBpm, int shift)
        => Assert.Equal(
            shift >= 0 ? Base << shift : Base >> -shift,
            HoldTickStepCalculator.Calculate(bpm, progressJudgeBpm, Base));

    [Fact]
    public void Calculate_ClampsRightShiftInsteadOfFallingToZero()
    {
        // 比值 256 时 480 >> 8 恰好还是 1；再往下旧实现会移成 0，让调用方“逐 tick 推进”的循环失去推进性。
        Assert.Equal(1, HoldTickStepCalculator.Calculate(240d / 256, 240d, Base));
        Assert.Equal(1, HoldTickStepCalculator.Calculate(240d / 4096, 240d, Base));
    }

    [Fact]
    public void Calculate_ClampsLeftShiftInsteadOfOverflowingNegative()
        => Assert.Equal(int.MaxValue, HoldTickStepCalculator.Calculate(240d * 1e9, 240d, Base));

    [Fact]
    public void Calculate_FallsBackToBaselineForIllegalInputs()
    {
        Assert.Equal(Base, HoldTickStepCalculator.Calculate(0d, 0d, Base));
        Assert.Equal(Base, HoldTickStepCalculator.Calculate(0d, 240d, Base));
        Assert.Equal(Base, HoldTickStepCalculator.Calculate(240d, 0d, Base));
        Assert.Equal(Base, HoldTickStepCalculator.Calculate(double.NaN, double.NaN, Base));
    }

    [Theory]
    [InlineData(1f, true)]
    [InlineData(240f, true)]
    [InlineData(0f, false)]
    [InlineData(-1f, false)]
    [InlineData(0.5f, false)]
    [InlineData(float.NaN, false)]
    [InlineData(float.PositiveInfinity, false)]
    [InlineData(float.NegativeInfinity, false)]
    public void IsValidProgJudgeBpm_EnforcesTheLowerThreshold(float value, bool expected)
        => Assert.Equal(expected, HoldTickStepCalculator.IsValidProgJudgeBpm(value));

    [Fact]
    public void CoerceProgJudgeBpm_KeepsValidValuesAndFallsBackTo240()
    {
        Assert.Equal(120f, HoldTickStepCalculator.CoerceProgJudgeBpm(120f));
        Assert.Equal(240f, HoldTickStepCalculator.CoerceProgJudgeBpm(0f));
        Assert.Equal(240f, HoldTickStepCalculator.CoerceProgJudgeBpm(0.5f));
        Assert.Equal(240f, HoldTickStepCalculator.CoerceProgJudgeBpm(float.NaN));
    }
}

public class HoldJudgeTickAdvancementTests
{
    public HoldJudgeTickAdvancementTests() => Log.Initialize(new Log([]));

    private static (OngekiFumen Fumen, Hold Hold) CreateHold(int endUnit)
    {
        var fumen = new OngekiFumen();
        var hold = new Hold { TGrid = TGrid.Zero };
        hold.SetHoldEnd(new HoldEnd { TGrid = new TGrid(endUnit) });
        fumen.AddObject(hold);
        return (fumen, hold);
    }

    [Fact]
    public void TickSequenceKeepsAdvancing_WhenProgJudgeBpmIsZero()
    {
        var (fumen, hold) = CreateHold(1);

        // 只取前几个：旧实现在步长为 0 时会无限产出同一个 tick，这里用 Take 把失败变成确定性的断言失败，
        // 而不是让测试自己去 OOM。
        var ticks = hold
            .CalculateJudgeTGrid(TGrid.Zero, TGrid.MaxValue, fumen.BpmList, 0f)
            .Take(8)
            .ToArray();

        Assert.NotEmpty(ticks);
        for (var i = 1; i < ticks.Length; i++)
            Assert.True(
                ticks[i].TotalGrid > ticks[i - 1].TotalGrid,
                $"第 {i} 个 tick 没有推进：{ticks[i - 1].TotalGrid} -> {ticks[i].TotalGrid}");
    }

    [Fact]
    public void TickSequenceMatchesTheLegacyClosedForm_ForNormalInputs()
    {
        var (fumen, hold) = CreateHold(1);

        var ticks = hold
            .CalculateJudgeTGrid(TGrid.Zero, TGrid.MaxValue, fumen.BpmList, 240f)
            .Select(x => x.TotalGrid)
            .ToArray();

        Assert.Equal(new[] { 480, 960, 1440, 1920 }, ticks);
    }
}
