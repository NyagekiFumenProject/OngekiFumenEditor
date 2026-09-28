#nullable enable

using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Base.Collections;

/// <summary>
/// ISF 归属语义：区域按开始时间升序检查、最先命中的区域生效；
/// 区域 T 区间为 [TGrid, EndIndicator.TGrid)，结束边界不包含。
/// </summary>
public sealed class IndividualSoflanAreaQueryTests
{
    [Fact]
    public void OverlappingAreas_EarliestStartingAreaWins()
    {
        var map = new IndividualSoflanAreaListMap();
        // 先开始的大区域
        map.Add(CreateArea(startUnit: 10, startGrid: 0, lengthGrids: 2000, centerX: 0, width: 96, group: 1));
        // 后开始、与前者局部重叠的小区域
        map.Add(CreateArea(startUnit: 10, startGrid: 720, lengthGrids: 360, centerX: 0, width: 48, group: 2));

        // 查询点同时落在两个区域内：取更早开始的 group 1，而不是后开始的 group 2
        Assert.Equal(1, map.QuerySoflanGroup(new XGrid(-24, 0), new TGrid(10, 720)));
    }

    [Fact]
    public void AreaBounds_TimeEndExclusive_XRangeInclusive()
    {
        var map = new IndividualSoflanAreaListMap();
        map.Add(CreateArea(startUnit: 10, startGrid: 720, lengthGrids: 360, centerX: 0, width: 48, group: 2));

        // 区域内部：X 两端对 XGrid（-24 / 24）均为闭区间
        Assert.Equal(2, map.QuerySoflanGroup(new XGrid(-24, 0), new TGrid(10, 900)));
        Assert.Equal(2, map.QuerySoflanGroup(new XGrid(24, 0), new TGrid(10, 900)));
        Assert.Equal(0, map.QuerySoflanGroup(new XGrid(-25, 0), new TGrid(10, 900)));

        // T 区间为 [10,720, 10,1080)：结束边界上的物体不属于该区域
        Assert.Equal(2, map.QuerySoflanGroup(new XGrid(0, 0), new TGrid(10, 1079)));
        Assert.Equal(0, map.QuerySoflanGroup(new XGrid(0, 0), new TGrid(10, 1080)));
    }

    private static IndividualSoflanArea CreateArea(float startUnit, int startGrid, int lengthGrids, float centerX, int width, int group)
    {
        var area = new IndividualSoflanArea
        {
            SoflanGroup = group
        };
        area.TGrid.Unit = startUnit;
        area.TGrid.Grid = startGrid;
        area.XGrid.Unit = centerX - width / 2;
        area.EndIndicator.XGrid.Unit = centerX + width / 2;
        area.EndIndicator.TGrid = area.TGrid + new GridOffset(0, lengthGrids);
        return area;
    }
}
