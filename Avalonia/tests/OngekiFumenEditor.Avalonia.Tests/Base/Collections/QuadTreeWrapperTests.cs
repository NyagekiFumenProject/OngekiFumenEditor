#nullable enable

using System.ComponentModel;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.Collections.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Base.Collections;

public sealed class QuadTreeWrapperTests
{
    [Fact]
    public void EmptyWrapper_ReadsAndDebugsWithoutBuilding()
    {
        var wrapper = CreateWrapper();
        var unregistered = new RectangleItem(0, 0, 1, 1);

        Assert.Equal(0, wrapper.Count);
        Assert.Empty(wrapper);
        Assert.Empty(wrapper.Query(0, 0));
        Assert.Null(wrapper.DebugFindDataQueryPath(unregistered));
        wrapper.DebugDump();
    }

    [Fact]
    public void Remove_RemovesRegisteredObjectFromEveryReadPath()
    {
        var wrapper = CreateWrapper();
        var removed = new RectangleItem(0, 0, 2, 2);
        var retained = new RectangleItem(10, 10, 12, 12);
        var neverRegistered = new RectangleItem(20, 20, 21, 21);

        wrapper.Add(removed);
        wrapper.Add(retained);

        Assert.Equal(2, wrapper.Count);
        Assert.Same(removed, Assert.Single(wrapper.Query(1, 1)));
        Assert.Same(retained, Assert.Single(wrapper.Query(11, 11)));

        wrapper.Remove(removed);

        Assert.Equal(1, wrapper.Count);
        Assert.Same(retained, Assert.Single(wrapper));
        Assert.Empty(wrapper.Query(1, 1));
        Assert.Same(retained, Assert.Single(wrapper.Query(11, 11)));
        Assert.Null(wrapper.DebugFindDataQueryPath(removed));

        wrapper.Remove(removed);
        wrapper.Remove(neverRegistered);
        Assert.Equal(1, wrapper.Count);
        Assert.Same(retained, Assert.Single(wrapper.Query(11, 11)));

        wrapper.Remove(retained);

        Assert.Equal(0, wrapper.Count);
        Assert.Empty(wrapper);
        Assert.Empty(wrapper.Query(11, 11));
        Assert.Null(wrapper.DebugFindDataQueryPath(retained));
    }

    [Fact]
    public void RelevantPropertyChange_RebuildsUsingNewBounds()
    {
        var wrapper = CreateWrapper();
        var item = new RectangleItem(0, 0, 1, 1);
        wrapper.Add(item);

        Assert.Same(item, Assert.Single(wrapper.Query(0.5f, 0.5f)));

        item.XStart = 10;
        item.XEnd = 11;
        item.YStart = 10;
        item.YEnd = 11;

        Assert.Empty(wrapper.Query(0.5f, 0.5f));
        Assert.Same(item, Assert.Single(wrapper.Query(10.5f, 10.5f)));
    }

    [Fact]
    public void ReversedAndDegenerateBounds_AreNormalizedAndInclusive()
    {
        var wrapper = CreateWrapper();
        var reversed = new RectangleItem(4, 4, 1, 1);
        var point = new RectangleItem(7, 7, 7, 7);
        wrapper.Add(reversed);
        wrapper.Add(point);

        Assert.Same(reversed, Assert.Single(wrapper.Query(1, 1)));
        Assert.Same(reversed, Assert.Single(wrapper.Query(4, 4)));
        Assert.Same(reversed, Assert.Single(wrapper.Query(2.5f, 2.5f)));
        Assert.Same(point, Assert.Single(wrapper.Query(7, 7)));
        Assert.Empty(wrapper.Query(0.999f, 1));
        Assert.Empty(wrapper.Query(7, 6.999f));
        Assert.Equal(2, wrapper.Count);
        Assert.Equal(2, wrapper.ToArray().Distinct().Count());
    }

    [Fact]
    public void NineDegenerateObjects_StopAtDepthLimitAndRemainQueryable()
    {
        var wrapper = CreateWrapper();
        var items = Enumerable.Range(0, 9)
            .Select(_ => new RectangleItem(3, 3, 3, 3))
            .ToArray();

        foreach (var item in items)
            wrapper.Add(item);

        var queried = wrapper.Query(3, 3).ToArray();

        Assert.Equal(9, wrapper.Count);
        Assert.Equal(9, queried.Length);
        Assert.Equal(9, queried.Distinct().Count());
        foreach (var item in items)
            Assert.Contains(item, queried);
    }

    [Fact]
    public void IndividualSoflanAreaMap_RemoveDoesNotReturnDeletedArea()
    {
        var map = new IndividualSoflanAreaListMap();
        var area = new IndividualSoflanArea
        {
            SoflanGroup = 7,
            TGrid = new TGrid(0),
            XGrid = new XGrid(-1)
        };
        area.EndIndicator.TGrid = new TGrid(4);
        area.EndIndicator.XGrid = new XGrid(1);

        map.Add(area);
        Assert.Equal(7, map.QuerySoflanGroup(new XGrid(0), new TGrid(2)));

        map.Remove(area);

        Assert.Equal(0, map.QuerySoflanGroup(new XGrid(0), new TGrid(2)));
        Assert.Null(map.DebugFindDataQueryPath(area));
    }

    private static NotQuadTreeWrapper<float, float, RectangleItem> CreateWrapper() => new(
        x => x.XStart,
        x => x.YStart,
        x => x.XEnd,
        x => x.YEnd,
        1f,
        1f,
        nameof(RectangleItem.XStart),
        nameof(RectangleItem.YStart),
        nameof(RectangleItem.XEnd),
        nameof(RectangleItem.YEnd));

    private sealed class RectangleItem : INotifyPropertyChanged
    {
        private float xStart;
        private float yStart;
        private float xEnd;
        private float yEnd;

        public RectangleItem(float xStart, float yStart, float xEnd, float yEnd)
        {
            this.xStart = xStart;
            this.yStart = yStart;
            this.xEnd = xEnd;
            this.yEnd = yEnd;
        }

        public float XStart
        {
            get => xStart;
            set => SetValue(ref xStart, value, nameof(XStart));
        }

        public float YStart
        {
            get => yStart;
            set => SetValue(ref yStart, value, nameof(YStart));
        }

        public float XEnd
        {
            get => xEnd;
            set => SetValue(ref xEnd, value, nameof(XEnd));
        }

        public float YEnd
        {
            get => yEnd;
            set => SetValue(ref yEnd, value, nameof(YEnd));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void SetValue(ref float field, float value, string propertyName)
        {
            if (field == value)
                return;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
