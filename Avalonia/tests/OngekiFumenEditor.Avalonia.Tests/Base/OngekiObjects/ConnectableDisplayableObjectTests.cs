using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Base.OngekiObjects;

public sealed class ConnectableDisplayableObjectTests
{
    [Fact]
    public void ChildGetDisplayableObjects_ReturnsControlsThenChildOnce()
    {
        var chain = CreateChain();

        var actual = chain.First.GetDisplayableObjects().ToArray();

        AssertSameSequence(actual, chain.FirstControl, chain.SecondControl, chain.First);
    }

    [Fact]
    public void StartGetDisplayableObjects_EmptyChildren_ReturnsOnlyStart()
    {
        var start = new LaneLeftStart
        {
            RecordId = 1,
            TGrid = new TGrid(0),
            XGrid = new XGrid(0)
        };

        var actual = start.GetDisplayableObjects().ToArray();

        AssertSameSequence(actual, start);
    }

    [Fact]
    public void StartGetDisplayableObjects_ReturnsEveryMemberOnceInStableOrder()
    {
        var chain = CreateChain();

        var actual = chain.Start.GetDisplayableObjects().ToArray();

        AssertSameSequence(
            actual,
            chain.Start,
            chain.FirstControl,
            chain.SecondControl,
            chain.First,
            chain.Second);
    }

    [Fact]
    public void FumenGetAllDisplayableObjects_DoesNotDuplicateConnectableChildren()
    {
        var chain = CreateChain();
        var fumen = new OngekiFumen();
        fumen.AddObject(chain.Start);

        var expected = new IDisplayableObject[]
        {
            chain.Start,
            chain.FirstControl,
            chain.SecondControl,
            chain.First,
            chain.Second
        };

        AssertSameSequence(GetConnectableDisplayables(fumen), expected);
        AssertSameSequence(
            GetConnectableDisplayables(fumen, new TGrid(0), new TGrid(5)),
            expected);
    }

    [Fact]
    public void ReverseSelectionOverConnectableAggregate_TogglesEachObjectOnce()
    {
        var chain = CreateChain();
        var fumen = new OngekiFumen();
        fumen.AddObject(chain.Start);

        var displayables = GetConnectableDisplayables(fumen, new TGrid(0), new TGrid(5))
            .OfType<ISelectableObject>()
            .ToArray();

        Assert.Equal(5, displayables.Length);
        Assert.All(displayables, selectable => Assert.False(selectable.IsSelected));

        foreach (var selectable in displayables)
            selectable.IsSelected = !selectable.IsSelected;

        Assert.All(displayables, selectable => Assert.True(selectable.IsSelected));
    }

    private static IReadOnlyList<IDisplayableObject> GetConnectableDisplayables(
        OngekiFumen fumen,
        TGrid? min = null,
        TGrid? max = null)
    {
        var displayables = min is null || max is null
            ? fumen.GetAllDisplayableObjects()
            : fumen.GetAllDisplayableObjects(min, max);
        return displayables.Where(x => x is ConnectableObjectBase or LaneCurvePathControlObject).ToArray();
    }

    private static void AssertSameSequence(
        IReadOnlyList<IDisplayableObject> actual,
        params IDisplayableObject[] expected)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
            Assert.Same(expected[i], actual[i]);

        for (var i = 0; i < actual.Count; i++)
        {
            for (var j = i + 1; j < actual.Count; j++)
                Assert.False(ReferenceEquals(actual[i], actual[j]));
        }
    }

    private static (LaneLeftStart Start,
        LaneLeftNext First,
        LaneLeftNext Second,
        LaneCurvePathControlObject FirstControl,
        LaneCurvePathControlObject SecondControl) CreateChain()
    {
        var start = new LaneLeftStart
        {
            RecordId = 1,
            TGrid = new TGrid(0),
            XGrid = new XGrid(0)
        };
        var first = new LaneLeftNext
        {
            TGrid = new TGrid(2),
            XGrid = new XGrid(2),
            CurvePrecision = 0.25f
        };
        var firstControl = new LaneCurvePathControlObject
        {
            TGrid = new TGrid(0.5f),
            XGrid = new XGrid(1)
        };
        var secondControl = new LaneCurvePathControlObject
        {
            TGrid = new TGrid(1),
            XGrid = new XGrid(1.5f)
        };
        first.AddControlObject(firstControl);
        first.AddControlObject(secondControl);

        var second = new LaneLeftNext
        {
            TGrid = new TGrid(4),
            XGrid = new XGrid(4)
        };
        start.AddChildObject(first);
        start.AddChildObject(second);

        return (start, first, second, firstControl, secondControl);
    }
}
