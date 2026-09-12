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
/// Contract for <see cref="DrawTimeSignatureHelper"/>: rendering must size and enumerate from the
/// current drawing context, never from the design-mode-only <c>RectInDesignMode</c> cache.
/// </summary>
public sealed class DrawTimeSignatureHelperTests
{
    [AvaloniaFact]
    public void DrawLines_PreviewWithoutDesignRect_UsesCurrentViewportWidth()
    {
        // RectInDesignMode is never assigned in this flow, so it stays default(VisibleRect) with zero size.
        var host = new TimeSignatureHost(preview: true, viewWidth: 800, viewHeight: 600, viewOriginY: 0);

        var result = Run(host, drawText: false);

        Assert.True(result.HasLines, "preview must still emit beat lines when no design rect was ever set");
        Assert.Equal(800f, result.MaxLineX, 3);
    }

    [AvaloniaFact]
    public void DrawLines_DesignMode_IgnoresStaleDesignRect()
    {
        var host = new TimeSignatureHost(preview: false, viewWidth: 800, viewHeight: 600, viewOriginY: 0);
        // A stale design rect pointing at an off-screen empty window must not suppress the lines.
        host.Editor.RectInDesignMode = new VisibleRect(
            new OpenTkVector2(800, -100000),
            new OpenTkVector2(0, -99000));

        var result = Run(host, drawText: false);

        Assert.True(result.HasLines, "design mode must enumerate from the current WorldRect");
        Assert.Equal(800f, result.MaxLineX, 3);
    }

    [AvaloniaFact]
    public void DrawTimeSigntureText_YIsViewRelative()
    {
        var host = new TimeSignatureHost(preview: true, viewWidth: 800, viewHeight: 600, viewOriginY: 120);

        var result = Run(host, drawText: true);

        Assert.True(result.HasLines);
        Assert.NotEmpty(result.TextY);
        // World Y is already de-globalized for the lines; the cached label must use the same space.
        Assert.Equal(result.FirstLineY + 10f, result.TextY[0], 3);
    }

    private static TimeSignatureResult Run(TimeSignatureHost host, bool drawText)
    {
        var helper = new DrawTimeSignatureHelper();
        helper.Initalize(null!);

        using var builder = new DrawCommandListBuilder();
        helper.DrawLines(host, builder);
        if (drawText)
            helper.DrawTimeSigntureText(host, builder);

        using var commandList = builder.GetDrawCommandList();
        var line = commandList.Commands.OfType<DrawSimpleLinesCommand>().SingleOrDefault();
        if (line is null)
            return new TimeSignatureResult(false, 0, float.NaN, float.NaN, Array.Empty<float>());

        var maxX = float.MinValue;
        for (var i = 0; i < line.Points.Count; i++)
            maxX = Math.Max(maxX, line.Points[i].Point.X);

        var textY = commandList.Commands.OfType<DrawStringCommand>().Select(command => command.Position.Y).ToArray();
        return new TimeSignatureResult(true, line.Points.Count, maxX, line.Points[0].Point.Y, textY);
    }

    private readonly record struct TimeSignatureResult(
        bool HasLines,
        int LineVertexCount,
        float MaxLineX,
        float FirstLineY,
        float[] TextY);

    private sealed class TimeSignatureHost : IFumenEditorDrawingContext
    {
        public TimeSignatureHost(bool preview, float viewWidth, float viewHeight, float viewOriginY)
        {
            var fumen = new OngekiFumen();
            Editor = new FumenVisualEditorViewModel
            {
                ViewWidth = viewWidth,
                ViewHeight = viewHeight,
                EditorContext = new EditorContext { Fumen = fumen }
            };
            Editor.IsLocked = preview; // hiding editor objects is what selects preview mode

            CurrentDrawingTargetContext = new DrawingTargetContext
            {
                CurrentSoflanList = fumen.SoflansMap.DefaultSoflanList,
                ViewRelativeRect = new VisibleRect(new OpenTkVector2(viewWidth, 0), new OpenTkVector2(0, viewHeight)),
                WorldRect = new VisibleRect(new OpenTkVector2(viewWidth, viewOriginY), new OpenTkVector2(0, viewOriginY + viewHeight)),
                ViewRelativeOriginY = viewOriginY,
                ViewWidth = viewWidth,
                ViewHeight = viewHeight
            };
        }

        public FumenVisualEditorViewModel Editor { get; }
        public DrawingTargetContext CurrentDrawingTargetContext { get; }
        public TimeSpan CurrentPlayTime => TimeSpan.Zero;
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
