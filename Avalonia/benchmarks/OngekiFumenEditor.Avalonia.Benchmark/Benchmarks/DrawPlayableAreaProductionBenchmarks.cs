using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Benchmark.Infrastructure;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.Editors;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Models.Settings;
using NumericsVector2 = System.Numerics.Vector2;
using OpenTkVector2 = OpenTK.Mathematics.Vector2;

namespace OngekiFumenEditor.Avalonia.Benchmark.Benchmarks;

/// <summary>
/// Drives the real <see cref="DrawPlayableAreaHelper.DrawPlayField"/> over a synthetic chart whose
/// wall/child counts are controlled by <see cref="WallCount"/> and <see cref="ChildrenPerWall"/>.
///
/// This is the production counterpart of the standalone boundary variants in
/// <c>DrawPlayableAreaHelperNewP1Benchmarks</c>: it measures the actual editor path so the effect of
/// a fix to PERF-RND-009 / RND-10 (samples × lanes × children scans) can be read off a before/after run
/// of the same benchmark.
/// </summary>
[MemoryDiagnoser]
public class DrawPlayableAreaProductionBenchmarks
{
    private const int RangeSpanUnits = 96;

    /// <summary>Total number of wall lanes (split between left and right).</summary>
    [Params(2, 8)]
    public int WallCount { get; set; }

    /// <summary>Number of child nodes per wall lane; each becomes a boundary sample.</summary>
    [Params(16, 64, 256)]
    public int ChildrenPerWall { get; set; }

    private PlayFieldHost host = null!;
    private DrawPlayableAreaHelper helper = null!;
    private DrawCommandListBuilder builder = null!;
    private TGrid rangeMin = null!;
    private TGrid rangeMax = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        BenchmarkRuntime.EnsureInitialized();

        var res = (int)TGrid.DEFAULT_RES_T;
        var fumen = BuildChart(WallCount, ChildrenPerWall, RangeSpanUnits, res);

        rangeMin = TGrid.FromTotalGrid(0);
        rangeMax = TGrid.FromTotalGrid(RangeSpanUnits * res);

        host = new PlayFieldHost(fumen);

        EditorGlobalSetting.Default.EnablePlayFieldDrawing = true;
        helper = new DrawPlayableAreaHelper();
        helper.Initalize(null!);
        builder = new DrawCommandListBuilder(new StubStringMeasure());
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        builder.Dispose();
        helper.Dispose();
        EditorGlobalSetting.Default.EnablePlayFieldDrawing = false;
    }

    [Benchmark]
    [STAThread]
    public int DrawPlayField()
    {
        helper.DrawPlayField(host, builder, rangeMin, rangeMax);

        using var commandList = builder.GetDrawCommandList();
        var vertexCount = 0;
        foreach (var command in commandList.Commands)
        {
            if (command is DrawPolygonCommand polygon)
                vertexCount += polygon.Vertices.Count;
        }

        return vertexCount;
    }

    private static OngekiFumen BuildChart(int wallCount, int childrenPerWall, int spanUnits, int res)
    {
        var fumen = new OngekiFumen();
        var leftCount = wallCount / 2;
        var rightCount = wallCount - leftCount;
        var recordId = 1;

        for (var i = 0; i < leftCount; i++)
            AddWall(fumen, recordId++, left: true, i, childrenPerWall, spanUnits, res);

        for (var i = 0; i < rightCount; i++)
            AddWall(fumen, recordId++, left: false, i, childrenPerWall, spanUnits, res);

        fumen.Setup();
        return fumen;
    }

    private static void AddWall(
        OngekiFumen fumen,
        int recordId,
        bool left,
        int wallIndex,
        int childCount,
        int spanUnits,
        int res)
    {
        // Left walls march outward to -25, -26, ...; right walls to +25, +26, ...,
        // so the merged boundary is driven by the outermost lane and every child contributes a sample.
        var slope = left ? -1d : 1d;
        var baseX = (left ? -24d : 24d) + slope * wallIndex;

        LaneStartBase start = left
            ? new WallLeftStart { RecordId = recordId, TGrid = TGrid.FromTotalGrid(0), XGrid = new XGrid((float)baseX) }
            : new WallRightStart { RecordId = recordId, TGrid = TGrid.FromTotalGrid(0), XGrid = new XGrid((float)baseX) };

        for (var i = 1; i <= childCount; i++)
        {
            var tGrid = TGrid.FromTotalGrid(i * spanUnits * res / childCount);
            var xGrid = new XGrid((float)(baseX + slope * i));

            ConnectableChildObjectBase child = left
                ? new WallLeftNext { TGrid = tGrid, XGrid = xGrid }
                : new WallRightNext { TGrid = tGrid, XGrid = xGrid };

            start.AddChildObject(child);
        }

        fumen.AddObject(start);
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

    private sealed class PlayFieldHost : IFumenEditorDrawingContext
    {
        public PlayFieldHost(OngekiFumen fumen, float viewWidth = 800, float viewMinY = 0, float viewMaxY = 1200)
        {
            Editor = new FumenVisualEditorViewModel
            {
                ViewWidth = viewWidth,
                ViewHeight = viewMaxY - viewMinY,
                EditorContext = new EditorContext { Fumen = fumen }
            };
            Editor.IsLocked = true;

            CurrentDrawingTargetContext = new DrawingTargetContext
            {
                CurrentSoflanList = fumen.SoflansMap.DefaultSoflanList,
                ViewRelativeRect = new VisibleRect(new OpenTkVector2(viewWidth, viewMinY), new OpenTkVector2(0, viewMaxY)),
                ViewRelativeOriginY = 0,
                ViewWidth = viewWidth,
                ViewHeight = viewMaxY - viewMinY
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

        // Identity mapping keeps the benchmark's screen-distance resampling inert so sample count is
        // exactly the number of wall nodes in the range.
        public double ConvertToY(double tGridUnit, SoflanList soflans) => tGridUnit;

        public void Render(TimeSpan ts)
        {
        }
    }
}
