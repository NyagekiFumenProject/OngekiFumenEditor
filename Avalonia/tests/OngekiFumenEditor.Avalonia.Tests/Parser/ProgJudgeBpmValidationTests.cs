using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenMetaInfoBrowser.ViewModels;
using OngekiFumenEditor.Avalonia.Parser.Ogkr;
using OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl.MetaInfo;
using OngekiFumenEditor.Avalonia.Utils;
using Xunit;
using NyagekiProgJudgeBpmHeader = OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki.CommandImpl.Headers.ProgJudgeBpm;

namespace OngekiFumenEditor.Avalonia.Tests.Parser;

public class ProgJudgeBpmValidationTests
{
    public ProgJudgeBpmValidationTests() => Log.Initialize(new Log([]));

    [Theory]
    [InlineData("0", 240f)]
    [InlineData("-240", 240f)]
    [InlineData("0.5", 240f)]
    [InlineData("NaN", 240f)]
    [InlineData("Infinity", 240f)]
    [InlineData("180", 180f)]
    [InlineData("240", 240f)]
    public void OgkrParser_CoercesProgJudgeBpm(string raw, float expected)
    {
        var fumen = new OngekiFumen();

        new ProgJudgeBpmCommandParsers()
            .ParseMetaInfo(new CommandArgs { Line = $"PROGJUDGE_BPM\t{raw}" }, fumen);

        Assert.Equal(expected, fumen.MetaInfo.ProgJudgeBpm);
    }

    [Theory]
    [InlineData("0", 240f)]
    [InlineData("-1", 240f)]
    [InlineData("0.5", 240f)]
    [InlineData("NaN", 240f)]
    [InlineData("180", 180f)]
    public void NyagekiHeader_CoercesProgJudgeBpm(string raw, float expected)
    {
        var fumen = new OngekiFumen();

        new NyagekiProgJudgeBpmHeader().ParseAndApply(fumen, ["Header.ProgJudgeBpm", raw]);

        Assert.Equal(expected, fumen.MetaInfo.ProgJudgeBpm);
    }

    [Theory]
    [InlineData(0f, 240f)]
    [InlineData(-1f, 240f)]
    [InlineData(0.5f, 240f)]
    [InlineData(float.NaN, 240f)]
    [InlineData(float.PositiveInfinity, 240f)]
    [InlineData(180f, 180f)]
    public void UiModelProxy_CoercesProgJudgeBpm(float raw, float expected)
    {
        var fumen = new OngekiFumen();

        new OngekiFumenModelProxy(fumen).ProgJudgeBpm = raw;

        Assert.Equal(expected, fumen.MetaInfo.ProgJudgeBpm);
    }

    [Fact]
    public void CalculateObjectStatistics_ReturnsPromptly_WhenProgJudgeBpmIsZero()
    {
        var fumen = new OngekiFumen();
        fumen.MetaInfo.ProgJudgeBpm = 0f;

        var hold = new Hold { TGrid = TGrid.Zero };
        hold.SetHoldEnd(new HoldEnd { TGrid = new TGrid(1) });
        fumen.AddObject(hold);

        // 若回归，这个线程会一直自旋、测试必然超时失败（这也是唯一能观察到“静默卡死”的方式）。
        var statistics = Task.Run(() => FumenStatisticsCalculator.CalculateObjectStatisticsAsync(fumen));

        Assert.True(
            statistics.Wait(TimeSpan.FromSeconds(15)),
            "统计计算 15s 内没有返回：疑似 PROGJUDGE_BPM 非法值让 tick 步长循环失去推进性（PERF-PRS-001）");

        // 非法 PROGJUDGE_BPM 走“不缩放”分支：步长 = TRESOLUTION>>2 = 480，1920 / 480 = 4 个 tick。
        Assert.Equal(4, statistics.Result.HoldObjects);
    }
}
