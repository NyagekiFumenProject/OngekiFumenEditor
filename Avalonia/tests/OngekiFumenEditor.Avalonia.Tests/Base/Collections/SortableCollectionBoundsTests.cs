using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Base.Collections;

/// <summary>
/// Covers the lower/upper bound helpers added for PERF-DAT-010 / DAT-13 to
/// <see cref="SortableCollection{T,X}"/>.
///
/// They exist because <c>BinarySearchBy</c> answers a different question: on a duplicate run it walks forward
/// and returns the <b>last equal</b> index — the dense-key scan called out by PERF-DAT-002 / DAT-02 — whereas
/// a predecessor/successor query needs "first index &gt;= key" and "first index &gt; key". These tests pin the
/// new semantics <i>and</i> the fact that the pre-existing <c>BinarySearchBy</c> behaviour was left untouched.
/// </summary>
public sealed class SortableCollectionBoundsTests
{
    private const int Unit = (int)TGrid.DEFAULT_RES_T;

    private static MeterChange Meter(int totalGrid) => new() { TGrid = TGrid.FromTotalGrid(totalGrid) };

    private static TGrid At(int totalGrid) => TGrid.FromTotalGrid(totalGrid);

    /// <summary>Inserts in the given (deliberately unordered) order; <c>Add</c> keeps the collection ascending.</summary>
    private static TGridSortList<MeterChange> Sorted(params int[] totalGrids)
    {
        var list = new TGridSortList<MeterChange>();
        foreach (var totalGrid in totalGrids)
            list.Add(Meter(totalGrid));
        return list;
    }

    [Fact]
    public void Bounds_OnEmptyCollection_AreZero()
    {
        var list = Sorted();

        Assert.Equal(0, list.LowerBoundIndex(At(0)));
        Assert.Equal(0, list.UpperBoundIndex(At(0)));
        Assert.Equal(0, list.LowerBoundIndex(At(-Unit)));
        Assert.Equal(0, list.UpperBoundIndex(At(Unit)));
    }

    [Theory]
    [InlineData(-Unit)]
    [InlineData(0)]
    [InlineData(Unit)]
    [InlineData(Unit * 2)]
    [InlineData(Unit * 3)]
    [InlineData(Unit * 4)]
    [InlineData(Unit * 1000)]
    public void Bounds_EqualCountOfElementsLessOrEqual(int probe)
    {
        var list = Sorted(Unit * 3, Unit, Unit * 2); // inserted out of order on purpose

        var keys = new[] { Unit, Unit * 2, Unit * 3 };
        var time = At(probe);

        // lower bound == number of elements strictly before `time`
        Assert.Equal(keys.Count(k => At(k) < time), list.LowerBoundIndex(time));
        // upper bound == number of elements <= `time`
        Assert.Equal(keys.Count(k => At(k) <= time), list.UpperBoundIndex(time));
    }

    [Fact]
    public void Bounds_OnDuplicateRun_StraddleTheWholeRun()
    {
        var list = Sorted(Unit, Unit * 2, Unit * 2, Unit * 2, Unit * 3);

        Assert.Equal(1, list.LowerBoundIndex(At(Unit * 2)));
        Assert.Equal(4, list.UpperBoundIndex(At(Unit * 2)));

        Assert.Equal(0, list.LowerBoundIndex(At(Unit)));
        Assert.Equal(1, list.UpperBoundIndex(At(Unit)));

        Assert.Equal(4, list.LowerBoundIndex(At(Unit * 3)));
        Assert.Equal(5, list.UpperBoundIndex(At(Unit * 3)));
    }

    [Fact]
    public void Bounds_AllElementsBelowOrAboveKey_SaturateAtTheEnds()
    {
        var list = Sorted(Unit * 2, Unit * 4);

        Assert.Equal(0, list.LowerBoundIndex(At(Unit)));
        Assert.Equal(0, list.UpperBoundIndex(At(Unit)));
        Assert.Equal(0, list.LowerBoundIndex(At(0)));

        Assert.Equal(1, list.LowerBoundIndex(At(Unit * 4)));
        Assert.Equal(2, list.UpperBoundIndex(At(Unit * 4)));

        Assert.Equal(2, list.LowerBoundIndex(At(Unit * 5)));
        Assert.Equal(2, list.UpperBoundIndex(At(Unit * 5)));
    }

    [Fact]
    public void Bounds_AreConsistentWithTheIndexer()
    {
        var list = Sorted(Unit * 5, Unit, Unit * 3);

        var lower = list.LowerBoundIndex(At(Unit * 3));
        var upper = list.UpperBoundIndex(At(Unit * 3));

        Assert.True(lower < upper);
        for (var i = lower; i < upper; i++)
            Assert.Equal(At(Unit * 3).TotalGrid, list[i].TGrid.TotalGrid);
    }

    [Fact]
    public void BinarySearchBy_StillReturnsLastEqualIndex_SoTheOldContractIsIntact()
    {
        var list = Sorted(Unit, Unit * 2, Unit * 2, Unit * 2, Unit * 3);

        // Pre-existing behaviour, deliberately unchanged by this work.
        Assert.Equal(0, list.BinarySearchBy(At(Unit)));
        Assert.Equal(3, list.BinarySearchBy(At(Unit * 2)));
        Assert.Equal(4, list.BinarySearchBy(At(Unit * 3)));

        // Missing key -> `~insertionPoint`.
        var missing = list.BinarySearchBy(At(Unit * 5));
        Assert.True(missing < 0);
        Assert.Equal(5, ~missing);

        Assert.Equal(4, list.BinaryFindLastIndexByKey(At(Unit * 3)));

        // Pinning a pre-existing quirk so it doesn't get silently relied upon: for a missing key this returns
        // the insertion point even when that equals Count, i.e. NOT a usable index. Out of scope to change.
        Assert.Equal(5, list.BinaryFindLastIndexByKey(At(Unit * 4)));
    }
}
