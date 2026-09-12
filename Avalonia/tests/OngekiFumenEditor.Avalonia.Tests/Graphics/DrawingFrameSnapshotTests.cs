using Avalonia.Headless.XUnit;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.Editors;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using Xunit;
using NumericsVector2 = System.Numerics.Vector2;
using OpenTkVector2 = OpenTK.Mathematics.Vector2;

namespace OngekiFumenEditor.Avalonia.Tests.Graphics;

/// <summary>
/// Rendering must read the per-frame time snapshot from <see cref="DrawingTargetContext"/> instead of
/// the live editor time, which the UI thread advances while the render thread draws.
/// </summary>
public sealed class DrawingFrameSnapshotTests
{
    [AvaloniaFact]
    public void FrameSnapshot_ComesFromDrawingContext_NotLiveEditorTime()
    {
        var snapshotTGrid = TGrid.FromTotalGrid(12345);
        var host = new SnapshotHost();
        host.CurrentDrawingTargetContext = new DrawingTargetContext
        {
            CurrentTime = TimeSpan.FromSeconds(3),
            CurrentTGrid = snapshotTGrid,
            CurrentSoflanList = host.Editor.EditorContext.Fumen.SoflansMap.DefaultSoflanList
        };

        Assert.Equal(TimeSpan.FromSeconds(3), ((IFumenEditorDrawingContext)host).FrameTime);
        Assert.Same(snapshotTGrid, ((IFumenEditorDrawingContext)host).FrameTGrid);
    }

    [AvaloniaFact]
    public void FrameSnapshot_FallsBackToEditorWhenNotPopulated()
    {
        var host = new SnapshotHost();
        host.CurrentDrawingTargetContext = new DrawingTargetContext();
        var target = (IFumenEditorDrawingContext)host;

        Assert.Equal(target.CurrentPlayTime, target.FrameTime);
        Assert.Equal(host.Editor.GetCurrentTGrid()?.TotalGrid, target.FrameTGrid?.TotalGrid);
    }

    [AvaloniaFact]
    public void DrawJudgeLine_UsesFrameSnapshot_NotLiveEditorTime()
    {
        var snapshotTGrid = TGrid.FromTotalGrid(5000);
        var host = new SnapshotHost();
        host.CurrentDrawingTargetContext = new DrawingTargetContext
        {
            CurrentTime = TimeSpan.FromSeconds(5),
            CurrentTGrid = snapshotTGrid,
            CurrentSoflanList = host.Editor.EditorContext.Fumen.SoflansMap.DefaultSoflanList,
            ViewRelativeRect = new VisibleRect(new OpenTkVector2(host.Editor.ViewWidth, 0), new OpenTkVector2(0, 600)),
            ViewRelativeOriginY = 1000
        };

        var helper = new DrawJudgeLineHelper();
        helper.Initalize(null!);

        using var builder = new DrawCommandListBuilder();
        helper.Draw(host, builder);
        using var commandList = builder.GetDrawCommandList();

        var line = commandList.Commands.OfType<DrawSimpleLinesCommand>().Single();
        var snapshotY = (float)(snapshotTGrid.TotalUnit - host.CurrentDrawingTargetContext.ViewRelativeOriginY);
        var liveY = (float)(host.Editor.GetCurrentTGrid()!.TotalUnit - host.CurrentDrawingTargetContext.ViewRelativeOriginY);

        Assert.Equal(snapshotY, line.Points[0].Point.Y, 3);
        Assert.NotEqual(liveY, line.Points[0].Point.Y);
    }

    private sealed class SnapshotHost : IFumenEditorDrawingContext
    {
        public SnapshotHost()
        {
            Editor = new FumenVisualEditorViewModel
            {
                ViewWidth = 800,
                ViewHeight = 600,
                EditorContext = new EditorContext { Fumen = new OngekiFumen() }
            };
            Editor.IsLocked = true; // preview mode
        }

        public FumenVisualEditorViewModel Editor { get; }
        public DrawingTargetContext CurrentDrawingTargetContext { get; set; } = new();
        public TimeSpan CurrentPlayTime => Editor.CurrentPlayTime;
        public IPerfomenceMonitor PerfomenceMonitor { get; } = new DummyPerformenceMonitor();
        public IRenderContext RenderContext => null!;

        public void RegisterSelectableObject(OngekiObjectBase obj, NumericsVector2 centerPos, NumericsVector2 size)
        {
        }

        public bool CheckDrawingVisible(DrawingVisible visible) => true;
        public bool CheckVisible(TGrid tGrid) => true;
        public bool CheckRangeVisible(TGrid minTGrid, TGrid maxTGrid) => true;
        public double ConvertToY(double tGridUnit, SoflanList soflans) => tGridUnit;

        public void Render(TimeSpan ts)
        {
        }
    }
}
