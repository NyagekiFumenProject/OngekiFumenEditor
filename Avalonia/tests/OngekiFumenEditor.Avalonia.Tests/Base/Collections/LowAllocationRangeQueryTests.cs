using OngekiFumenEditor.Avalonia.Base.Collections.Base;
using OngekiFumenEditor.Avalonia.Base.Collections.Base.RangeTree;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Base.Collections;

public sealed class LowAllocationRangeQueryTests
{
    [Fact]
    public void SortableCollection_BinaryFindRange_PreservesClosedRangeSemantics()
    {
        var collection = new SortableCollection<int, int>(value => value);

        Assert.Empty(collection.BinaryFindRange(0, 10));

        collection.Add(0);
        collection.Add(1);
        collection.Add(2);
        collection.Add(2);
        collection.Add(3);

        Assert.Equal(new[] { 0, 1, 2, 2, 3 }, collection.BinaryFindRange(0, 3));
        Assert.Equal(new[] { 1 }, collection.BinaryFindRange(1, 1));
        Assert.Empty(collection.BinaryFindRange(4, 5));
    }

    [Fact]
    public void IntervalTree_QueryInto_AppendsInQueryOrder()
    {
        var tree = CreateTree();
        var expected = tree.Query(3, 5).ToArray();
        var output = new List<string> { "sentinel" };

        tree.QueryInto(3, 5, output);

        Assert.Equal(new[] { "B", "A" }, expected);
        Assert.Equal("sentinel", output[0]);
        Assert.Equal(expected, output.Skip(1));
    }

    [Fact]
    public void IntervalTree_EarlyRangeQueryDisposal_DoesNotAffectNextQuery()
    {
        var tree = CreateTree();

        Assert.Equal(new[] { "B" }, tree.Query(0, 12).Take(1));

        var complete = tree.Query(0, 12).ToArray();
        Assert.Equal(new[] { "B", "A", "C" }, complete);
        Assert.Equal(1, complete.Count(value => value == "A"));
        Assert.Equal(1, complete.Count(value => value == "B"));
        Assert.Equal(1, complete.Count(value => value == "C"));
    }

    private static IntervalTree<int, string> CreateTree()
    {
        var tree = new IntervalTree<int, string>();
        tree.Add(0, 5, "A");
        tree.Add(3, 7, "B");
        tree.Add(10, 12, "C");
        return tree;
    }
}
