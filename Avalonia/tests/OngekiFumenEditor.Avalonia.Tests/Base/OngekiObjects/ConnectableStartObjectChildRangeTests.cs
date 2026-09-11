using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Base.OngekiObjects;

/// <summary>
/// Guards the allocation-free <see cref="ConnectableStartObject.TryGetValidPathChildRange"/> path so it
/// keeps selecting exactly the same children as <see cref="ConnectableStartObject.GetChildObjectsFromTGrid"/>.
/// </summary>
public sealed class ConnectableStartObjectChildRangeTests
{
    private const int Res = (int)TGrid.DEFAULT_RES_T;

    [Fact]
    public void TryGetValidPathChildRange_MatchesGetChildObjectsFromTGrid_ForMultiChildLane()
    {
        var lane = new WallLeftStart { TGrid = TGrid.FromTotalGrid(0), XGrid = new XGrid(-2) };
        lane.AddChildObject(new WallLeftNext { TGrid = TGrid.FromTotalGrid(5 * Res), XGrid = new XGrid(-5) });
        lane.AddChildObject(new WallLeftNext { TGrid = TGrid.FromTotalGrid(5 * Res), XGrid = new XGrid(-15) });
        lane.AddChildObject(new WallLeftNext { TGrid = TGrid.FromTotalGrid(10 * Res), XGrid = new XGrid(-30) });

        Assert.True(lane.IsPathVaild());
        AssertRangeParity(lane);
    }

    [Fact]
    public void TryGetValidPathChildRange_MatchesGetChildObjectsFromTGrid_ForSingleChildLane()
    {
        var lane = new WallRightStart { TGrid = TGrid.FromTotalGrid(0), XGrid = new XGrid(2) };
        lane.AddChildObject(new WallRightNext { TGrid = TGrid.FromTotalGrid(5 * Res), XGrid = new XGrid(10) });

        Assert.True(lane.IsPathVaild());
        AssertRangeParity(lane);
    }

    private static void AssertRangeParity(ConnectableStartObject lane)
    {
        // Cover before-start, exact nodes (including the duplicate-TGrid node), between nodes and past-the-end.
        for (var t = -2 * Res; t <= 12 * Res; t += Res / 4)
        {
            var tGrid = TGrid.FromTotalGrid(t);
            var expected = lane.GetChildObjectsFromTGrid(tGrid).ToArray();
            var actual = lane.TryGetValidPathChildRange(tGrid, out var start, out var count)
                ? EnumerateRange(lane, start, count)
                : Array.Empty<ConnectableChildObjectBase>();

            Assert.Equal(expected, actual);
        }
    }

    private static ConnectableChildObjectBase[] EnumerateRange(ConnectableStartObject lane, int start, int count)
    {
        var result = new ConnectableChildObjectBase[count];
        for (var i = 0; i < count; i++)
            result[i] = lane.GetChildObjectAt(start + i);
        return result;
    }
}
