using Avalonia.Headless.XUnit;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.Editors;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Models.Settings;
using Xunit;
using NumericsVector2 = System.Numerics.Vector2;
using OpenTkVector2 = OpenTK.Mathematics.Vector2;

namespace OngekiFumenEditor.Avalonia.Tests.Graphics;

/// <summary>
/// Behaviour contract for <see cref="DrawPlayableAreaHelper.DrawPlayField"/>: which wall boundary
/// is chosen at each timeline sample, how samples are connected, clipped and merged, and which
/// inputs produce no geometry at all. Assertions read the emitted polygon vertices, i.e. what the
/// renderer actually consumes.
/// </summary>
public sealed class DrawPlayableAreaHelperTests
{
    private const int TGridRes = (int)TGrid.DEFAULT_RES_T;
    private const double DefaultLeftUnit = -24;
    private const double DefaultRightUnit = 24;

    // ---------- early return / gating ----------

    [AvaloniaFact]
    public void DrawPlayField_WhenSettingDisabled_ProducesNoPolygon()
    {
        var host = new PlayFieldHost(new OngekiFumen());

        var result = RunPlayField(host, 0, 10, enable: false);

        Assert.False(result.HasPolygon);
    }

    [AvaloniaFact]
    public void DrawPlayField_WhenEditorIsDesignMode_ProducesNoPolygon()
    {
        var host = new PlayFieldHost(new OngekiFumen());
        host.Editor.IsLocked = false; // design mode

        var result = RunPlayField(host, 0, 10);

        Assert.False(result.HasPolygon);
    }

    [AvaloniaFact]
    public void DrawPlayField_WhenRangeIsInverted_ProducesNoPolygon()
    {
        var result = RunPlayField(new PlayFieldHost(new OngekiFumen()), 10, 0);

        Assert.False(result.HasPolygon);
    }

    [AvaloniaFact]
    public void DrawPlayField_WhenRangeCollapsesToSingleSample_ProducesNoPolygon()
    {
        // min == max == current time, so only one distinct sample exists.
        var result = RunPlayField(new PlayFieldHost(new OngekiFumen()), 0, 0);

        Assert.False(result.HasPolygon);
    }

    // ---------- default boundaries ----------

    [AvaloniaFact]
    public void DrawPlayField_WithNoWalls_UsesDefaultBoundaryPair()
    {
        var host = new PlayFieldHost(new OngekiFumen());

        var result = RunPlayField(host, 0, 10);

        Assert.True(result.HasPolygon);
        Assert.Equal(Primitive.Triangles, result.Primitive);
        Assert.Equal(6, result.Vertices.Length);

        Assert.Equal(X(host, DefaultLeftUnit), result.Vertices[0].Point.X, 4);
        Assert.Equal(X(host, DefaultRightUnit), result.Vertices[2].Point.X, 4);
        Assert.Equal(0f, result.Vertices[0].Point.Y, 4);
        Assert.Equal(10f, result.Vertices[1].Point.Y, 4);
    }

    [AvaloniaFact]
    public void DrawPlayField_EmitsSingleTriangulatedPolygonCommand()
    {
        var host = new PlayFieldHost(new OngekiFumen());

        var result = RunPlayField(host, 0, 10);

        Assert.True(result.HasPolygon);
        Assert.Equal(0, result.Vertices.Length % 3);
    }

    [AvaloniaFact]
    public void DrawPlayField_IsDeterministicAcrossRepeatedRuns()
    {
        var host = new PlayFieldHost(new OngekiFumen());
        AddLeftWall(host.Fumen, 1, (0, -10), (5, -30), (10, -10));

        var first = RunPlayField(host, 0, 10);
        var second = RunPlayField(host, 0, 10);

        Assert.Equal(first.Vertices, second.Vertices);
    }

    // ---------- single-side wall boundaries ----------

    [AvaloniaFact]
    public void DrawPlayField_LeftWall_SetsLeftBoundaryOnly()
    {
        var host = new PlayFieldHost(new OngekiFumen());
        AddLeftWall(host.Fumen, 1, (0, -30), (10, -30));

        var result = RunPlayField(host, 0, 10);

        AssertTrueLeftEdges(result, host, -30);
        AssertTrueRightEdges(result, host, DefaultRightUnit);
    }

    [AvaloniaFact]
    public void DrawPlayField_RightWall_SetsRightBoundaryOnly()
    {
        var host = new PlayFieldHost(new OngekiFumen());
        AddRightWall(host.Fumen, 1, (0, 30), (10, 30));

        var result = RunPlayField(host, 0, 10);

        AssertTrueLeftEdges(result, host, DefaultLeftUnit);
        AssertTrueRightEdges(result, host, 30);
    }

    [AvaloniaFact]
    public void DrawPlayField_MultipleLeftWalls_KeepsOutermostBoundary()
    {
        var host = new PlayFieldHost(new OngekiFumen());
        AddLeftWall(host.Fumen, 1, (0, -10), (10, -10));
        AddLeftWall(host.Fumen, 2, (0, -30), (10, -30));

        var result = RunPlayField(host, 0, 10);

        AssertTrueLeftEdges(result, host, -30);
    }

    [AvaloniaFact]
    public void DrawPlayField_MultipleRightWalls_KeepsOutermostBoundary()
    {
        var host = new PlayFieldHost(new OngekiFumen());
        AddRightWall(host.Fumen, 1, (0, 10), (10, 10));
        AddRightWall(host.Fumen, 2, (0, 30), (10, 30));

        var result = RunPlayField(host, 0, 10);

        AssertTrueRightEdges(result, host, 30);
    }

    [AvaloniaFact]
    public void DrawPlayField_OverlappingLeftWalls_MergePerSample()
    {
        var host = new PlayFieldHost(new OngekiFumen());
        AddLeftWall(host.Fumen, 1, (0, -10), (5, -10));  // active over [0,5]
        AddLeftWall(host.Fumen, 2, (5, -30), (10, -30)); // active over [5,10]

        var result = RunPlayField(host, 0, 10);

        Assert.Equal(X(host, -10), LeftXAt(result, 0), 4);
        Assert.Equal(X(host, -30), LeftXAt(result, 10), 4);
    }

    [AvaloniaFact]
    public void DrawPlayField_LeftAndRightWalls_AreIndependent()
    {
        var host = new PlayFieldHost(new OngekiFumen());
        AddLeftWall(host.Fumen, 1, (0, -30), (10, -30));
        AddRightWall(host.Fumen, 2, (0, 12), (10, 12));

        var result = RunPlayField(host, 0, 10);

        AssertTrueLeftEdges(result, host, -30);
        AssertTrueRightEdges(result, host, 12);
    }

    // ---------- interpolation and samples at wall nodes ----------

    [AvaloniaFact]
    public void DrawPlayField_SlopedWall_InterpolatesAtRangeEnds()
    {
        var host = new PlayFieldHost(new OngekiFumen());
        AddLeftWall(host.Fumen, 1, (0, -10), (10, -30));

        var result = RunPlayField(host, 0, 10);

        // The quad spans the whole range: starts from the wall x at t=0 (-10) and ends at the child node (-30).
        // A triangulated quad repeats its corners, so assert on the distinct X values of each row instead
        // of expecting a single vertex per boundary.
        var fromRowXs = result.Vertices
            .Where(v => NearlyEqual(v.Point.Y, 0f))
            .Select(v => Round(v.Point.X))
            .ToHashSet();
        Assert.Contains(Round(X(host, -10)), fromRowXs);
        Assert.Contains(Round(X(host, DefaultRightUnit)), fromRowXs);

        var toRowXs = result.Vertices
            .Where(v => NearlyEqual(v.Point.Y, 10f))
            .Select(v => Round(v.Point.X))
            .ToHashSet();
        Assert.Contains(Round(X(host, -30)), toRowXs);
        Assert.Contains(Round(X(host, DefaultRightUnit)), toRowXs);
    }

    [AvaloniaFact]
    public void DrawPlayField_WallChildNode_AddsBoundarySampleAtNodeTime()
    {
        var host = new PlayFieldHost(new OngekiFumen());
        AddLeftWall(host.Fumen, 1, (0, -10), (5, -30), (10, -10));

        var result = RunPlayField(host, 0, 10);

        // A sample must exist exactly at the child node t=5, carrying the wall x=-30.
        Assert.Contains(result.Vertices, v =>
            NearlyEqual(v.Point.Y, 5f) && NearlyEqual(v.Point.X, (float)X(host, -30)));
    }

    [AvaloniaFact]
    public void DrawPlayField_TwoChildrenAtSameNode_PrevTakesFirstAndNextTakesLast()
    {
        var host = new PlayFieldHost(new OngekiFumen());
        AddLeftWall(host.Fumen, 1, (0, -2), (5, -5), (5, -15), (10, -30));

        var result = RunPlayField(host, 0, 10);
        var y5 = result.Vertices.Where(v => NearlyEqual(v.Point.Y, 5f)).Select(v => (double)v.Point.X).ToArray();

        // Arriving at the node uses the first child (-5); leaving it uses the last child (-15).
        Assert.Contains(X(host, -5), y5.Select(x => Round(x)));
        Assert.Contains(X(host, -15), y5.Select(x => Round(x)));
    }

    [AvaloniaFact]
    public void DrawPlayField_WallStartingInsideRange_EntersAtItsMinBoundary()
    {
        var host = new PlayFieldHost(new OngekiFumen());
        AddLeftWall(host.Fumen, 1, (5, -30), (10, -30));

        var result = RunPlayField(host, 0, 10);

        Assert.Equal(X(host, DefaultLeftUnit), LeftXAt(result, 0), 4);
        Assert.Equal(X(host, -30), LeftXAt(result, 10), 4);
    }

    [AvaloniaFact]
    public void DrawPlayField_WallEndingInsideRange_LeavesAtItsMaxBoundary()
    {
        var host = new PlayFieldHost(new OngekiFumen());
        AddLeftWall(host.Fumen, 1, (0, -30), (5, -30));

        var result = RunPlayField(host, 0, 10);

        Assert.Equal(X(host, -30), LeftXAt(result, 0), 4);
        Assert.Equal(X(host, DefaultLeftUnit), LeftXAt(result, 10), 4);
    }

    [AvaloniaFact]
    public void DrawPlayField_WallOutsideRange_IsIgnored()
    {
        var host = new PlayFieldHost(new OngekiFumen());
        AddLeftWall(host.Fumen, 1, (20, -30), (30, -30));

        var result = RunPlayField(host, 0, 10);

        AssertTrueLeftEdges(result, host, DefaultLeftUnit);
    }

    [AvaloniaFact]
    public void DrawPlayField_ChildlessWall_ContributesNothing()
    {
        var host = new PlayFieldHost(new OngekiFumen());
        AddLeftWall(host.Fumen, 1, (5, -30));

        var result = RunPlayField(host, 0, 10);

        AssertTrueLeftEdges(result, host, DefaultLeftUnit);
    }

    // ---------- merge / resample / clip ----------

    [AvaloniaFact]
    public void DrawPlayField_CollinearSamples_AreMergedButRangeEndpointsRetained()
    {
        var host = new PlayFieldHost(new OngekiFumen());

        // Screen distance > 32 forces resampling; constant boundaries then collapse back to one quad.
        var result = RunPlayField(host, 0, 100);

        Assert.True(result.HasPolygon);
        Assert.Equal(6, result.Vertices.Length);
        Assert.Equal(0f, result.Vertices[0].Point.Y, 4);
        Assert.Equal(100f, result.Vertices[1].Point.Y, 4);
    }

    [AvaloniaFact]
    public void DrawPlayField_CurvedWall_ProducesMultipleQuads()
    {
        var host = new PlayFieldHost(new OngekiFumen());
        var lane = AddLeftWallWithCurve(host.Fumen, 1, (0, 0), (100, 0), control: (50, 40));
        Assert.True(lane.IsPathVaild());

        var result = RunPlayField(host, 0, 100);

        Assert.True(result.HasPolygon);
        Assert.True(result.Vertices.Length > 6, $"expected more than one quad, got {result.Vertices.Length} vertices");
        Assert.All(result.Vertices, v =>
        {
            Assert.InRange(v.Point.Y, 0f, 100f);
            Assert.InRange(v.Point.X, (float)X(host, -1), (float)X(host, 40));
        });
    }

    [AvaloniaFact]
    public void DrawPlayField_ClipsGeometryToVisibleY()
    {
        var host = new PlayFieldHost(new OngekiFumen(), viewMinY: 20, viewMaxY: 80);

        var result = RunPlayField(host, 0, 100);

        Assert.True(result.HasPolygon);
        Assert.All(result.Vertices, v => Assert.InRange(v.Point.Y, 20f, 80f));
        Assert.Contains(result.Vertices, v => NearlyEqual(v.Point.Y, 20f));
        Assert.Contains(result.Vertices, v => NearlyEqual(v.Point.Y, 80f));
    }

    [AvaloniaFact]
    public void DrawPlayField_InvalidPathWall_DoesNotThrowAndStaysWithinEnvelope()
    {
        var host = new PlayFieldHost(new OngekiFumen());
        // Control point beyond the child produces a TGrid reversal => IsPathVaild() == false.
        var lane = AddLeftWallWithCurve(host.Fumen, 1, (0, -2), (10, -10), control: (20, -40));
        Assert.False(lane.IsPathVaild());

        var result = RunPlayField(host, 0, 10);

        Assert.True(result.HasPolygon);
        Assert.All(result.Vertices, v => Assert.InRange(v.Point.Y, 0f, 15f));
    }

    // ---------- helpers ----------

    private static void AssertTrueLeftEdges(PlayFieldResult result, PlayFieldHost host, double unit)
    {
        Assert.True(result.HasPolygon);
        // Left edge vertices are indices 0 and 1 within each 6-vertex quad; from/to left X repeats at 4.
        for (var i = 0; i + 5 < result.Vertices.Length; i += 6)
        {
            Assert.Equal(X(host, unit), result.Vertices[i].Point.X, 4);
            Assert.Equal(X(host, unit), result.Vertices[i + 1].Point.X, 4);
        }
    }

    private static void AssertTrueRightEdges(PlayFieldResult result, PlayFieldHost host, double unit)
    {
        Assert.True(result.HasPolygon);
        for (var i = 0; i + 5 < result.Vertices.Length; i += 6)
        {
            Assert.Equal(X(host, unit), result.Vertices[i + 2].Point.X, 4);
            Assert.Equal(X(host, unit), result.Vertices[i + 5].Point.X, 4);
        }
    }

    private static double LeftXAt(PlayFieldResult result, float y)
    {
        var vertex = result.Vertices.First(v => NearlyEqual(v.Point.Y, y));
        return vertex.Point.X;
    }

    private static double X(PlayFieldHost host, double xGridUnit)
        => XGridCalculator.ConvertXGridToX(xGridUnit, host.Editor);

    private static double Round(double value) => Math.Round(value, 4);

    private static bool NearlyEqual(float a, float b) => Math.Abs(a - b) <= 0.0005f;

    private static int Units(int units) => units * TGridRes;

    private static PlayFieldResult RunPlayField(PlayFieldHost host, int minUnits, int maxUnits, bool enable = true)
    {
        var original = EditorGlobalSetting.Default.EnablePlayFieldDrawing;
        EditorGlobalSetting.Default.EnablePlayFieldDrawing = enable;

        var helper = new DrawPlayableAreaHelper();
        helper.Initalize(null!);
        try
        {
            using var builder = new DrawCommandListBuilder(new StubStringMeasure());
            helper.DrawPlayField(
                host,
                builder,
                TGrid.FromTotalGrid(Units(minUnits)),
                TGrid.FromTotalGrid(Units(maxUnits)));

            using var commandList = builder.GetDrawCommandList();
            var polygon = commandList.Commands.OfType<DrawPolygonCommand>().SingleOrDefault();
            return polygon is null
                ? PlayFieldResult.None
                : new PlayFieldResult(true, polygon.Primitive, polygon.Vertices.ToArray());
        }
        finally
        {
            helper.Dispose();
            EditorGlobalSetting.Default.EnablePlayFieldDrawing = original;
        }
    }

    private static WallLeftStart AddLeftWall(OngekiFumen fumen, int recordId, params (int TotalGrid, double X)[] points)
        => AddWall<WallLeftStart, WallLeftNext>(fumen, recordId, points);

    private static WallRightStart AddRightWall(OngekiFumen fumen, int recordId, params (int TotalGrid, double X)[] points)
        => AddWall<WallRightStart, WallRightNext>(fumen, recordId, points);

    private static TStart AddWall<TStart, TChild>(OngekiFumen fumen, int recordId, (int TotalGrid, double X)[] points)
        where TStart : LaneStartBase, new()
        where TChild : ConnectableChildObjectBase, new()
    {
        var (startGrid, startX) = points[0];
        var lane = new TStart
        {
            RecordId = recordId,
            TGrid = TGrid.FromTotalGrid(Units(startGrid)),
            XGrid = new XGrid((float)startX)
        };

        foreach (var (grid, x) in points.Skip(1))
        {
            lane.AddChildObject(new TChild
            {
                TGrid = TGrid.FromTotalGrid(Units(grid)),
                XGrid = new XGrid((float)x)
            });
        }

        fumen.AddObject(lane);
        return lane;
    }

    private static WallLeftStart AddLeftWallWithCurve(
        OngekiFumen fumen,
        int recordId,
        (int TotalGrid, double X) start,
        (int TotalGrid, double X) end,
        (int TotalGrid, double X) control)
    {
        var child = new WallLeftNext
        {
            TGrid = TGrid.FromTotalGrid(Units(end.TotalGrid)),
            XGrid = new XGrid((float)end.X)
        };
        child.AddControlObject(new LaneCurvePathControlObject
        {
            TGrid = TGrid.FromTotalGrid(Units(control.TotalGrid)),
            XGrid = new XGrid((float)control.X)
        });

        var lane = new WallLeftStart
        {
            RecordId = recordId,
            TGrid = TGrid.FromTotalGrid(Units(start.TotalGrid)),
            XGrid = new XGrid((float)start.X)
        };
        lane.AddChildObject(child);
        fumen.AddObject(lane);
        return lane;
    }

    private readonly record struct PlayFieldResult(bool HasPolygon, Primitive Primitive, PolygonVertex[] Vertices)
    {
        public static PlayFieldResult None => new(false, default, Array.Empty<PolygonVertex>());
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

    /// <summary>
    /// Minimal <see cref="IFumenEditorDrawingContext"/> that backs the helper with a real editor and
    /// a deterministic identity TGrid-to-Y mapping, so assertions can reason in timeline units.
    /// </summary>
    private sealed class PlayFieldHost : IFumenEditorDrawingContext
    {
        public PlayFieldHost(OngekiFumen fumen, float viewWidth = 800, float viewMinY = 0, float viewMaxY = 600)
        {
            Fumen = fumen;
            Editor = new FumenVisualEditorViewModel
            {
                ViewWidth = viewWidth,
                ViewHeight = viewMaxY - viewMinY,
                EditorContext = new EditorContext { Fumen = fumen }
            };
            Editor.IsLocked = true; // unlocked object visibility == preview mode, so DrawPlayField is not skipped

            CurrentDrawingTargetContext = new DrawingTargetContext
            {
                CurrentSoflanList = fumen.SoflansMap.DefaultSoflanList,
                ViewRelativeRect = new VisibleRect(new OpenTkVector2(viewWidth, viewMinY), new OpenTkVector2(0, viewMaxY)),
                ViewRelativeOriginY = 0,
                ViewWidth = viewWidth,
                ViewHeight = viewMaxY - viewMinY
            };
        }

        public OngekiFumen Fumen { get; }
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

        // Identity mapping: 1 TGrid unit == 1 screen pixel of Y.
        public double ConvertToY(double tGridUnit, SoflanList soflans) => tGridUnit;

        public void Render(TimeSpan ts)
        {
        }
    }
}
