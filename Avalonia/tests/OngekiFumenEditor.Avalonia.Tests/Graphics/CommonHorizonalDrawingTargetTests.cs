using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using Xunit;
using NumericsVector2 = System.Numerics.Vector2;
using OpenTkVector2 = OpenTK.Mathematics.Vector2;

namespace OngekiFumenEditor.Avalonia.Tests.Graphics;

public sealed class CommonHorizonalDrawingTargetTests
{
    [Fact]
    public void DrawBatch_CoordinatesItemsAtSameTimelinePositionIntoOneStrip()
    {
        var tGrid = new TGrid(2, 0);
        OngekiTimelineObjectBase[] objects =
        [
            new BPMChange { TGrid = tGrid.CopyNew(), BPM = 180 },
            new MeterChange { TGrid = tGrid.CopyNew(), BunShi = 3, Bunbo = 4 }
        ];
        var context = new StubDrawingContext(isVisible: true);
        var target = new CommonHorizonalDrawingTarget();
        using var builder = new DrawCommandListBuilder(new StubStringMeasure());

        target.DrawBatch(context, builder, objects);
        using var commandList = builder.GetDrawCommandList();

        var lineCommand = Assert.Single(commandList.Commands.OfType<DrawSimpleLinesCommand>());
        Assert.Equal(4, lineCommand.Points.Count);
        Assert.Equal(0, lineCommand.Points[0].Point.X);
        Assert.Equal(60, lineCommand.Points[1].Point.X);
        Assert.Equal(60, lineCommand.Points[2].Point.X);
        Assert.Equal(120, lineCommand.Points[3].Point.X);

        var labels = commandList.Commands.OfType<DrawStringCommand>().Select(static command => command.Text).ToArray();
        Assert.Contains(" BPM:180 ", labels);
        Assert.Contains(" MET:3/4 ", labels);
        Assert.Contains("/", labels);
        Assert.Equal(objects, context.SelectableObjects);
    }

    [Fact]
    public void DrawBatch_WhenTimelinePositionIsOutsideView_KeepsOnlyRangeHeaders()
    {
        var context = new StubDrawingContext(isVisible: false);
        var target = new CommonHorizonalDrawingTarget();

        using (var hiddenBuilder = new DrawCommandListBuilder(new StubStringMeasure()))
        {
            target.DrawBatch(context, hiddenBuilder,
                [new BPMChange { TGrid = new TGrid(1, 0), BPM = 120 }]);
            using var hiddenCommands = hiddenBuilder.GetDrawCommandList();
            Assert.Empty(hiddenCommands.Commands);
        }

        var laneBlock = new LaneBlockArea { TGrid = new TGrid(1, 0) };
        using var headerBuilder = new DrawCommandListBuilder(new StubStringMeasure());
        target.DrawBatch(context, headerBuilder, [laneBlock]);
        using var headerCommands = headerBuilder.GetDrawCommandList();

        Assert.Single(headerCommands.Commands.OfType<DrawSimpleLinesCommand>());
        Assert.Contains(headerCommands.Commands.OfType<DrawStringCommand>(),
            static command => command.Text == " LBK:Left ");
        Assert.Contains(laneBlock, context.SelectableObjects);
    }

    private sealed class StubStringMeasure : IStringMeasure
    {
        public NumericsVector2 MeasureString(
            string text,
            NumericsVector2 scale,
            int fontSize,
            IStringDrawing.StringStyle style,
            IStringDrawing.IFontHandle handle) => new(text.Length * 8, 16);
    }

    private sealed class StubDrawingContext(bool isVisible) : IFumenEditorDrawingContext
    {
        public List<OngekiObjectBase> SelectableObjects { get; } = [];

        public TimeSpan CurrentPlayTime => TimeSpan.Zero;
        public FumenVisualEditorViewModel Editor => null!;
        public DrawingTargetContext CurrentDrawingTargetContext { get; } = new()
        {
            ViewRelativeRect = new VisibleRect(new OpenTkVector2(120, 0), new OpenTkVector2(0, 100)),
            ViewWidth = 120,
            ViewHeight = 100
        };
        public IPerfomenceMonitor PerfomenceMonitor { get; } = new DummyPerformenceMonitor();
        public IRenderContext RenderContext => null!;

        public void RegisterSelectableObject(OngekiObjectBase obj, NumericsVector2 centerPos, NumericsVector2 size) =>
            SelectableObjects.Add(obj);

        public bool CheckDrawingVisible(DrawingVisible visible) => true;
        public bool CheckVisible(TGrid tGrid) => isVisible;
        public bool CheckRangeVisible(TGrid minTGrid, TGrid maxTGrid) => isVisible;
        public double ConvertToY(double tGridUnit, SoflanList soflans) => tGridUnit;
        public double ConvertToViewRelativeY_DefaultSoflanGroup(TGrid tGrid) => tGrid.TotalGrid;

        public void Render(TimeSpan ts)
        {
        }
    }
}
