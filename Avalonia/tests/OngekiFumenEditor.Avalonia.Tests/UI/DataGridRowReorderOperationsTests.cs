using OngekiFumenEditor.Avalonia.UI.Behaviors;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.UI;

public sealed class DataGridRowReorderOperationsTests
{
    [Fact]
    public void Reorder_MultipleItemsBeforeTarget_PreservesSourceOrderOfMovingItems()
    {
        var source = CreateItems("A", "B", "C", "D", "E", "F");

        var result = DataGridRowReorderOperations.Reorder(
            source,
            [source[3], source[1]],
            source[4],
            DataGridRowDropPosition.Before);

        Assert.Equal(new[] { "A", "C", "B", "D", "E", "F" }, result.Select(static item => item.Id));
        Assert.Equal(new[] { "A", "B", "C", "D", "E", "F" }, source.Select(static item => item.Id));
    }

    [Fact]
    public void Reorder_MovingItemsAcrossTarget_AdjustsBoundaryAfterRemoval()
    {
        var source = CreateItems("A", "B", "C", "D", "E", "F");

        var result = DataGridRowReorderOperations.Reorder(
            source,
            [source[1], source[2]],
            source[4],
            DataGridRowDropPosition.After);

        Assert.Equal(new[] { "A", "D", "E", "B", "C", "F" }, result.Select(static item => item.Id));
        Assert.Equal(6, result.Distinct().Count());
    }

    [Fact]
    public void Reorder_MovingTargetWithSelection_KeepsStableRelativeOrder()
    {
        var source = CreateItems("A", "B", "C", "D", "E", "F");

        var result = DataGridRowReorderOperations.Reorder(
            source,
            [source[4], source[2]],
            source[4],
            DataGridRowDropPosition.Before);

        Assert.Equal(new[] { "A", "B", "D", "C", "E", "F" }, result.Select(static item => item.Id));
        Assert.Equal(source.Count, result.Count);
    }

    [Theory]
    [InlineData(DataGridRowDropPosition.Inside)]
    public void Reorder_Inside_ReturnsUnchangedCopy(DataGridRowDropPosition position)
    {
        var source = CreateItems("A", "B", "C", "D", "E");

        var result = DataGridRowReorderOperations.Reorder(source, [source[1]], source[3], position);

        Assert.Equal(source.Select(static item => item.Id), result.Select(static item => item.Id));
        Assert.NotSame(source, result);
    }

    [Fact]
    public void Reorder_MissingTarget_ReturnsUnchangedCopy()
    {
        var source = CreateItems("A", "B", "C", "D", "E");
        var missingTarget = new TestItem("missing-target");

        var result = DataGridRowReorderOperations.Reorder(
            source,
            [source[1], source[2]],
            missingTarget,
            DataGridRowDropPosition.After);

        Assert.Equal(source.Select(static item => item.Id), result.Select(static item => item.Id));
        Assert.NotSame(source, result);
    }

    [Fact]
    public void Reorder_MissingMovingItems_ReturnsUnchangedCopy()
    {
        var source = CreateItems("A", "B", "C", "D", "E");

        var result = DataGridRowReorderOperations.Reorder(
            source,
            [new TestItem("missing-moving")],
            source[3],
            DataGridRowDropPosition.Before);

        Assert.Equal(source.Select(static item => item.Id), result.Select(static item => item.Id));
        Assert.NotSame(source, result);
    }

    private static IReadOnlyList<TestItem> CreateItems(params string[] ids)
    {
        return ids.Select(static id => new TestItem(id)).ToArray();
    }

    private sealed record TestItem(string Id);
}
