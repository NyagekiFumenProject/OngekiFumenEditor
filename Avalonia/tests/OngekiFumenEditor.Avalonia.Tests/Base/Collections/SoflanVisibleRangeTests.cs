using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.EditorObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Utils.ObjectPool;
using Xunit;
using static OngekiFumenEditor.Avalonia.Base.Collections.SoflanList;

namespace OngekiFumenEditor.Avalonia.Tests.Base.Collections;

public sealed class SoflanVisibleRangeTests
{
    [Fact]
    public void PositiveSpeed_ReturnsExpectedVisibleRange()
    {
        var bpmList = new BpmList();
        var soflanList = new SoflanList();

        using var ranges = soflanList.GetVisibleRanges_PreviewMode(5_000, 1_000, 200, bpmList, 1);

        var range = Assert.Single(ranges);
        Assert.Equal(4.8d, range.minTGrid.TotalUnit, 6);
        Assert.Equal(5.8d, range.maxTGrid.TotalUnit, 6);
    }

    [Fact]
    public void ReverseStopAndBounce_ReturnSortedDisjointStableRanges()
    {
        var bpmList = new BpmList();
        var soflanList = new SoflanList(new ISoflan[]
        {
            new KeyframeSoflan { TGrid = new TGrid(0), Speed = 1 },
            new KeyframeSoflan { TGrid = new TGrid(4), Speed = -1 },
            new KeyframeSoflan { TGrid = new TGrid(8), Speed = 0 },
            new KeyframeSoflan { TGrid = new TGrid(10), Speed = 1 }
        });
        const double currentY = 1_000;
        const double viewHeight = 3_000;
        const double preOffset = 1_000;
        const double scale = 1;

        (double min, double max)[] expected;
        using (var ranges = soflanList.GetVisibleRanges_PreviewMode(currentY, viewHeight, preOffset, bpmList, scale))
        {
            Assert.NotEmpty(ranges);
            AssertSortedAndDisjoint(ranges);
            AssertVisibleKeyframesAreCovered(soflanList, bpmList, ranges, currentY, viewHeight, preOffset, scale);
            expected = ranges.Select(range => (range.minTGrid.TotalUnit, range.maxTGrid.TotalUnit)).ToArray();
        }

        using (var repeated = soflanList.GetVisibleRanges_PreviewMode(currentY, viewHeight, preOffset, bpmList, scale))
        {
            var actual = repeated.Select(range => (range.minTGrid.TotalUnit, range.maxTGrid.TotalUnit)).ToArray();
            Assert.Equal(expected, actual);
        }

        using var nextRental = ObjectPool.GetPooledList<VisibleTGridRange>();
        Assert.Empty(nextRental);
    }

    private static void AssertSortedAndDisjoint(IReadOnlyList<VisibleTGridRange> ranges)
    {
        for (var i = 0; i < ranges.Count; i++)
        {
            Assert.True(ranges[i].minTGrid <= ranges[i].maxTGrid);
            if (i > 0)
                Assert.True(ranges[i - 1].maxTGrid < ranges[i].minTGrid);
        }
    }

    private static void AssertVisibleKeyframesAreCovered(
        SoflanList soflanList,
        BpmList bpmList,
        IReadOnlyList<VisibleTGridRange> ranges,
        double currentY,
        double viewHeight,
        double preOffset,
        double scale)
    {
        var viewMinY = (currentY - preOffset) / scale;
        var viewMaxY = viewMinY + viewHeight / scale;

        foreach (var soflan in soflanList.OfType<KeyframeSoflan>())
        {
            var y = TGridCalculator.ConvertTGridToY_PreviewMode(soflan.TGrid, soflanList, bpmList, scale) / scale;
            if (y < viewMinY || y > viewMaxY)
                continue;

            Assert.Contains(ranges, range => range.minTGrid <= soflan.TGrid && soflan.TGrid <= range.maxTGrid);
        }
    }
}
