using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Caliburn.Micro;
using ControlzEx.Standard;
using Gemini.Framework;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Performence;
using OngekiFumenEditor.Kernel.Scheduler;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.UI.Controls;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Wpf;
using static OngekiFumenEditor.Kernel.Graphics.IDrawingContext;
using static OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors.DrawXGridHelper;
using Color = System.Drawing.Color;
using Vector4 = System.Numerics.Vector4;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

public partial class FumenVisualEditorViewModel : PersistedDocument, ISchedulable, IFumenEditorDrawingContext
{
    private static Dictionary<string, IFumenEditorDrawingTarget[]> drawTargets = new();
    private IPerfomenceMonitor actualPerformenceMonitor;

    private readonly List<CacheDrawXLineResult> cachedMagneticXGridLines = new();

    private Func<double, FumenVisualEditorViewModel, double>
        convertToY = TGridCalculator.ConvertTGridUnitToY_DesignMode;

    private string displayFPS = "";
    private readonly Dictionary<IFumenEditorDrawingTarget, IEnumerable<OngekiTimelineObjectBase>> drawMap = new();
    private IFumenEditorDrawingTarget[] drawTargetOrder;
    private readonly IPerfomenceMonitor dummyPerformenceMonitor = new DummyPerformenceMonitor();
    private bool enablePlayFieldDrawing;

    private bool isDisplayFPS;
    private DrawJudgeLineHelper judgeLineHelper;
    private DrawPlayableAreaHelper playableAreaHelper;
    private DrawPlayerLocationHelper playerLocationHelper;
    private Vector4 playFieldBackgroundColor;
    private int renderViewHeight;

    private int renderViewWidth;
    private DrawSelectingRangeHelper selectingRangeHelper;

    private readonly StringBuilder stringBuilder = new(2048);

    private DrawTimeSignatureHelper timeSignatureHelper;

    private float viewHeight;
    private float viewWidth;

    private readonly List<(TGrid minTGrid, TGrid maxTGrid)> visibleTGridRanges = new();
    private DrawXGridHelper xGridHelper;
    private int cacheMagaticXGridLinesHash;

    public IEnumerable<CacheDrawXLineResult> CachedMagneticXGridLines => cachedMagneticXGridLines;

    public PlayerLocationRecorder PlayerLocationRecorder { get; } = new();

    public bool IsDisplayFPS
    {
        get => isDisplayFPS;
        set
        {
            Set(ref isDisplayFPS, value);
            PerfomenceMonitor = value ? actualPerformenceMonitor : dummyPerformenceMonitor;
        }
    }

    private Stopwatch sw;
    private float actualRenderInterval;

    public string DisplayFPS
    {
        get => displayFPS;
        set
        {
            displayFPS = value;
            NotifyOfPropertyChange(() => DisplayFPS);
        }
    }

    public float ViewWidth
    {
        get => viewWidth;
        set
        {
            Set(ref viewWidth, value);
            RecalcViewProjectionMatrix();
        }
    }

    public float ViewHeight
    {
        get => viewHeight;
        set
        {
            Set(ref viewHeight, value);
            RecalcViewProjectionMatrix();
        }
    }

    public TimeSpan CurrentPlayTime { get; private set; } = TimeSpan.FromSeconds(0);

    public Matrix4 ViewMatrix { get; private set; }
    public Matrix4 ProjectionMatrix { get; private set; }
    public Matrix4 ViewProjectionMatrix { get; private set; }

    public FumenVisualEditorViewModel Editor => this;

    public VisibleRect Rect { get; set; }

    public IPerfomenceMonitor PerfomenceMonitor { get; private set; } = new DummyPerformenceMonitor();

    public void OnRenderSizeChanged(GLWpfControl glView, SizeChangedEventArgs sizeArg)
    {
        Log.LogDebug($"new size: {sizeArg.NewSize} , glView.RenderSize = {glView.RenderSize}");

        var dpiX = VisualTreeHelper.GetDpi(Application.Current.MainWindow).DpiScaleX;
        var dpiY = VisualTreeHelper.GetDpi(Application.Current.MainWindow).DpiScaleY;

        ViewWidth = (float)sizeArg.NewSize.Width;
        ViewHeight = (float)sizeArg.NewSize.Height;
        renderViewWidth = (int)(sizeArg.NewSize.Width * dpiX);
        renderViewHeight = (int)(sizeArg.NewSize.Height * dpiY);
    }

    public void LoadRenderOrderVisible()
    {
        var targets = drawTargets.Values.SelectMany(x => x).Distinct().ToArray();
        var map = new Dictionary<string, RenderTargetOrderVisible>();

        var json = EditorGlobalSetting.Default.RenderTargetOrderVisibleMap;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<string, RenderTargetOrderVisible>>(json);
                map = loaded;
            }
            catch (Exception e)
            {
                Log.LogError($"load json content failed:{e.Message}, drawing targets will use default configs.");
                map = new();
            }
        }

        foreach (var target in targets)
        {
            if (map.TryGetValue(target.GetType().Name, out var orderVisible))
            {
                target.CurrentRenderOrder = orderVisible.Order;
                target.Visible = orderVisible.Visible;
            }
            else
            {
                target.CurrentRenderOrder = target.DefaultRenderOrder;
                target.Visible = target.DefaultVisible;
            }
        }
        Log.LogInfo($"loaded.");
    }

    public void SaveRenderOrderVisible()
    {
        var targets = drawTargets.Values.SelectMany(x => x).Distinct().ToArray();
        var map = targets.ToDictionary(x => x.GetType().Name, x => new RenderTargetOrderVisible()
        {
            Order = x.CurrentRenderOrder,
            Visible = x.Visible
        });

        try
        {
            var json = JsonSerializer.Serialize(map);
            EditorGlobalSetting.Default.RenderTargetOrderVisibleMap = json;
            EditorGlobalSetting.Default.Save();
            Log.LogInfo($"saved.");
        }
        catch (Exception e)
        {
            Log.LogError($"save json content failed:{e.Message}");
            map = new();
        }
    }

    public async void PrepareRenderLoop(GLWpfControl openGLView)
    {
        Log.LogDebug("ready.");
        await IoC.Get<IDrawingManager>().CheckOrInitGraphics();

        var dpiX = VisualTreeHelper.GetDpi(Application.Current.MainWindow).DpiScaleX;
        var dpiY = VisualTreeHelper.GetDpi(Application.Current.MainWindow).DpiScaleY;

        ViewWidth = (float)openGLView.ActualWidth;
        ViewHeight = (float)openGLView.ActualHeight;

        renderViewWidth = (int)(openGLView.ActualWidth * dpiX);
        renderViewHeight = (int)(openGLView.ActualHeight * dpiY);

        playFieldBackgroundColor = Color.FromArgb(EditorGlobalSetting.Default.PlayFieldBackgroundColor).ToVector4();
        enablePlayFieldDrawing = EditorGlobalSetting.Default.EnablePlayFieldDrawing;

        drawTargets = IoC.GetAll<IFumenEditorDrawingTarget>()
            .SelectMany(target => target.DrawTargetID.Select(supportId => (supportId, target)))
            .GroupBy(x => x.supportId).ToDictionary(x => x.Key, x => x.Select(x => x.target).ToArray());

        LoadRenderOrderVisible();
        ResortRenderOrder();

        timeSignatureHelper = new DrawTimeSignatureHelper();
        xGridHelper = new DrawXGridHelper();
        judgeLineHelper = new DrawJudgeLineHelper();
        selectingRangeHelper = new DrawSelectingRangeHelper();
        playableAreaHelper = new DrawPlayableAreaHelper();
        playerLocationHelper = new DrawPlayerLocationHelper();

        actualPerformenceMonitor = IoC.Get<IPerfomenceMonitor>();
        IsDisplayFPS = IsDisplayFPS;

        UpdateActualRenderInterval();
        sw = new Stopwatch();
        sw.Start();

        openGLView.Render += OnEditorLoop;
    }

    private void OnEditorLoop(TimeSpan ts)
    {
        OnEditorUpdate(ts);
        OnEditorRender(ts);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Render(TimeSpan ts)
        => OnEditorRender(ts);

    private void UpdateActualRenderInterval()
    {
        actualRenderInterval = EditorGlobalSetting.Default.LimitFPS switch
        {
            <= 0 => 0,
            _ => 1000.0F / EditorGlobalSetting.Default.LimitFPS
        };
    }

    private void OnEditorRender(TimeSpan ts)
    {
#if DEBUG
        GLUtility.CheckError();
#endif
        //limit
        if (actualRenderInterval > 0)
        {
            var ms = sw.ElapsedMilliseconds;
            if (ms < actualRenderInterval)
                goto End;
            ts = TimeSpan.FromMilliseconds(ms);
            sw.Restart();
        }

        PerfomenceMonitor.PostUIRenderTime(ts);
        PerfomenceMonitor.OnBeforeRender();

        CleanRender();
        GL.Viewport(0, 0, renderViewWidth, renderViewHeight);

        hits.Clear();

        var fumen = Fumen;
        if (fumen is null)
            goto End;

        var tGrid = GetCurrentTGrid();

        var curY = ConvertToY(tGrid.TotalUnit);
        var minY = (float)(curY - Setting.JudgeLineOffsetY);
        var maxY = minY + ViewHeight;

        //计算可以显示的TGrid范围以及像素范围
        visibleTGridRanges.Clear();
        if (IsDesignMode)
        {
            var minTGrid = TGridCalculator.ConvertYToTGrid_DesignMode(minY, this) ?? TGrid.Zero;
            var maxTGrid = TGridCalculator.ConvertYToTGrid_DesignMode(maxY, this);

            if (maxTGrid is null || minTGrid is null)
                goto End;
            visibleTGridRanges.Add((minTGrid, maxTGrid));
        }
        else
        {
            var scale = Setting.VerticalDisplayScale;
            var ranges =
                Fumen.Soflans.GetVisibleRanges_PreviewMode(curY, ViewHeight, Setting.JudgeLineOffsetY, Fumen.BpmList,
                    scale);

            foreach (var x in ranges)
            {
                if (x.maxTGrid is null || x.minTGrid is null)
                    goto End;
                visibleTGridRanges.Add((x.minTGrid, x.maxTGrid));
            }
        }

        Rect = new VisibleRect(new Vector2(ViewWidth, minY), new Vector2(0, minY + ViewHeight));

        RecalculateMagaticXGridLines();

        foreach (var (minTGrid, maxTGrid) in visibleTGridRanges)
            playableAreaHelper.DrawPlayField(this, minTGrid, maxTGrid);
        playableAreaHelper.Draw(this);
        timeSignatureHelper.DrawLines(this);

        xGridHelper.DrawLines(this, CachedMagneticXGridLines);

        var map = ObjectPool<Dictionary<string, List<OngekiTimelineObjectBase>>>.Get();
        map.Clear();
        foreach (var obj in GetDisplayableObjects(fumen, visibleTGridRanges).OfType<OngekiTimelineObjectBase>())
        {
            if (!map.TryGetValue(obj.IDShortName, out var list))
            {
                list = map[obj.IDShortName] = ObjectPool<List<OngekiTimelineObjectBase>>.Get();
                list.Clear();
            }

            list.Add(obj);
        }

        foreach (var objGroup in map)
        {
            if (GetDrawingTarget(objGroup.Key) is not IFumenEditorDrawingTarget[] drawingTargets)
                continue;
            var list = objGroup.Value;

            foreach (var drawingTarget in drawingTargets)
                if (!drawMap.TryGetValue(drawingTarget, out var enums))
                    drawMap[drawingTarget] = list;
                else
                    drawMap[drawingTarget] = enums.Concat(list);
        }

        if (IsPreviewMode)
        {
            //特殊处理：子弹和Bell
            foreach (var drawingTarget in GetDrawingTarget(Bullet.CommandName))
                drawMap[drawingTarget] = Fumen.Bullets;
            foreach (var drawingTarget in GetDrawingTarget(Bell.CommandName))
                drawMap[drawingTarget] = Fumen.Bells;
        }

        var prevOrder = int.MinValue;
        foreach (var drawingTarget in drawTargetOrder.Where(x => CheckDrawingVisible(x.Visible)))
        {
            //check render order
            var order = drawingTarget.CurrentRenderOrder;
            if (prevOrder > order)
            {
                ResortRenderOrder();
                CleanRender();
                break;
            }

            prevOrder = order;

            if (drawMap.TryGetValue(drawingTarget, out var drawingObjs))
            {
                drawingTarget.Begin(this);
                //all object collection has been sorted within GetDisplayableObjects()
                foreach (var obj in drawingObjs/*.OrderBy(x => x.TGrid)*/)
                    drawingTarget.Post(obj);
                drawingTarget.End();
            }
        }

        timeSignatureHelper.DrawTimeSigntureText(this);
        xGridHelper.DrawXGridText(this, CachedMagneticXGridLines);
        judgeLineHelper.Draw(this);
        playerLocationHelper.Draw(this);
        selectingRangeHelper.Draw(this);

        //clean up
        foreach (var list in map.Values)
            ObjectPool<List<OngekiTimelineObjectBase>>.Return(list);
        ObjectPool<Dictionary<string, List<OngekiTimelineObjectBase>>>.Return(map);

    End:
        drawMap.Clear();
        PerfomenceMonitor.OnAfterRender();
    }

    public bool CheckDrawingVisible(DrawingVisible visible)
    {
        return visible.HasFlag(EditorObjectVisibility == Visibility.Visible
            ? DrawingVisible.Design
            : DrawingVisible.Preview);
    }

    public double ConvertToY(double tGridUnit)
    {
        return convertToY(tGridUnit, this);
    }

    public bool CheckVisible(TGrid tGrid)
    {
        foreach (var (minTGrid, maxTGrid) in visibleTGridRanges)
            if (minTGrid <= tGrid && tGrid <= maxTGrid)
                return true;
        return false;
    }

    public bool CheckRangeVisible(TGrid minTGrid, TGrid maxTGrid)
    {
        foreach (var visibleRange in visibleTGridRanges)
        {
            var result = !(minTGrid > visibleRange.maxTGrid || maxTGrid < visibleRange.minTGrid);
            if (result)
                return true;
        }

        return false;
    }

    public string SchedulerName => "Fumen Previewer Performance Statictis";

    public TimeSpan ScheduleCallLoopInterval => TimeSpan.FromSeconds(1);

    public void OnSchedulerTerm()
    {
    }

    public Task OnScheduleCall(CancellationToken cancellationToken)
    {
        if (IsDisplayFPS)
        {
            stringBuilder.Clear();

            PerfomenceMonitor?.FormatStatistics(stringBuilder);
#if DEBUG
            stringBuilder.AppendLine();
            stringBuilder.AppendLine($"View: {ViewWidth}x{ViewHeight}");
            stringBuilder.AppendLine($"VisibleYRange: [{Rect.MinY}, {Rect.MaxY}]");
            stringBuilder.AppendLine("VisibleTGridRanges:");
            foreach (var tGridRange in visibleTGridRanges.ToArray())
                stringBuilder.AppendLine($"*   {tGridRange.minTGrid}  -  {tGridRange.maxTGrid}");
#endif

            DisplayFPS = stringBuilder.ToString();

            PerfomenceMonitor?.Clear();
        }

        return Task.CompletedTask;
    }

    protected override void OnViewLoaded(object v)
    {
        base.OnViewLoaded(v);
        InitExtraMenuItems();
    }

    private void RecalcViewProjectionMatrix()
    {
        var xOffset = 0;

        var y = (float)convertToY(GetCurrentTGrid().TotalUnit, this);

        ProjectionMatrix =
            Matrix4.CreateOrthographic(ViewWidth, ViewHeight, -1, 1);
        ViewMatrix =
            Matrix4.CreateTranslation(new Vector3(-ViewWidth / 2 + xOffset,
                -y - ViewHeight / 2 + (float)Setting.JudgeLineOffsetY, 0));

        ViewProjectionMatrix = ViewMatrix * ProjectionMatrix;
    }

    private void ResortRenderOrder()
    {
        drawTargetOrder = drawTargets.Values.SelectMany(x => x).OrderBy(x => x.CurrentRenderOrder).Distinct().ToArray();
    }

    public IFumenEditorDrawingTarget[] GetDrawingTarget(string name)
    {
        return drawTargets.TryGetValue(name, out var drawingTarget) ? drawingTarget : default;
    }

    private IEnumerable<IDisplayableObject> GetDisplayableObjects(OngekiFumen fumen,
        IEnumerable<(TGrid min, TGrid max)> visibleRanges)
    {
        var containBeams = fumen.Beams.Any();

        var objects = visibleRanges.SelectMany(x =>
        {
            var (min, max) = x;
            var r = Enumerable.Empty<IDisplayableObject>()
                .Concat(fumen.Flicks.BinaryFindRange(min, max))
                .Concat(fumen.MeterChanges.Skip(1)) //not show first meter
                .Concat(fumen.BpmList.Skip(1)) //not show first bpm
                .Concat(fumen.ClickSEs.BinaryFindRange(min, max))
                .Concat(fumen.LaneBlocks.GetVisibleStartObjects(min, max))
                .Concat(fumen.Comments.BinaryFindRange(min, max))
                .Concat(fumen.Soflans.GetVisibleStartObjects(min, max))
                .Concat(fumen.EnemySets.BinaryFindRange(min, max))
                .Concat(fumen.Lanes.GetVisibleStartObjects(min, max))
                .Concat(fumen.Taps.BinaryFindRange(min, max))
                .Concat(fumen.Holds.GetVisibleStartObjects(min, max))
                .Concat(fumen.SvgPrefabs);

            if (containBeams)
            {
                var leadInTGrid = TGridCalculator.ConvertAudioTimeToTGrid(
                    TGridCalculator.ConvertTGridToAudioTime(min, this) -
                    TGridCalculator.ConvertFrameToAudioTime(BeamStart.LEAD_IN_DURATION_FRAME), this);
                var leadOutTGrid = TGridCalculator.ConvertAudioTimeToTGrid(
                    TGridCalculator.ConvertTGridToAudioTime(max, this) +
                    TimeSpan.FromMilliseconds(BeamStart.LEAD_OUT_DURATION), this);

                r = r.Concat(fumen.Beams.GetVisibleStartObjects(leadInTGrid, leadOutTGrid));
            }

            return r;
        });

        /*
         * 这里考虑到有spd<1的子弹/Bell会提前出现的情况，因此得分状态分别去选择
         */
        var objs = Enumerable.Empty<IDisplayableObject>();
        if (Editor.IsPreviewMode)
        {
            /*
            var r = fumen.Bells
                .AsEnumerable<IBulletPalleteReferencable>()
                .Concat(fumen.Bullets);

            objs = objs.Concat(r);
            */
        }
        else
        {
            foreach (var item in visibleRanges)
            {
                var (min, max) = item;
                var blts = fumen.Bullets.BinaryFindRange(min, max);
                var bels = fumen.Bells.BinaryFindRange(min, max);

                objs = objs.Concat(bels);
                objs = objs.Concat(blts);
            }
        }

        return objects.Concat(objs).SelectMany(x => x.GetDisplayableObjects());
    }

    private void CleanRender()
    {
        if (IsDesignMode || !enablePlayFieldDrawing)
            GL.ClearColor(16 / 255.0f, 16 / 255.0f, 16 / 255.0f, 1);
        else
            GL.ClearColor(playFieldBackgroundColor.X, playFieldBackgroundColor.Y, playFieldBackgroundColor.Z,
                playFieldBackgroundColor.W);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }

    public void OnLoaded(ActionExecutionContext e)
    {

    }

    private void RecalculateMagaticXGridLines()
    {
        var xOffset = (float)Setting.XOffset;
        var width = ViewWidth;
        if (width == 0)
        {
            cachedMagneticXGridLines.Clear();
            return;
        }
        var xUnitSpace = (float)Setting.XGridUnitSpace;
        var maxDisplayXUnit = Setting.XGridDisplayMaxUnit;

        //check if it is necessary to recalculate and generate
        var hash = HashCode.Combine(xOffset, width, xUnitSpace, maxDisplayXUnit);
        if (cacheMagaticXGridLinesHash == hash)
            return;
        cacheMagaticXGridLinesHash = hash;
        cachedMagneticXGridLines.Clear();

        var unitSize = (float)XGridCalculator.CalculateXUnitSize(maxDisplayXUnit, width, xUnitSpace);
        var totalUnitValue = 0f;

        var baseX = width / 2 + xOffset;

        var limitLength = width + Math.Abs(xOffset);

        for (var totalLength = baseX + unitSize; totalLength - xOffset < limitLength; totalLength += unitSize)
        {
            totalUnitValue += xUnitSpace;

            cachedMagneticXGridLines.Add(new CacheDrawXLineResult
            {
                X = totalLength,
                XGridTotalUnit = totalUnitValue,
                XGridTotalUnitDisplay = totalUnitValue.ToString()
            });

            cachedMagneticXGridLines.Add(new CacheDrawXLineResult
            {
                X = baseX - (totalLength - baseX),
                XGridTotalUnit = -totalUnitValue,
                XGridTotalUnitDisplay = (-totalUnitValue).ToString()
            });
        }

        cachedMagneticXGridLines.Add(new CacheDrawXLineResult
        {
            X = baseX,
            XGridTotalUnit = 0f
        });
    }

    public void OnSizeChanged(ActionExecutionContext e)
    {
        Log.LogInfo("resize");
        var scrollViewer = e.Source as AnimatedScrollViewer;
        scrollViewer?.InvalidateMeasure();
    }
}