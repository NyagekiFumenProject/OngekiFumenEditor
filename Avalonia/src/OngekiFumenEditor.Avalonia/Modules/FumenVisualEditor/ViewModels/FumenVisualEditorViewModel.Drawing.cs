using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Views;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.Collections.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;
using OngekiFumenEditor.Avalonia.Kernel.Scheduler;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.Editors;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.ObjectPool;
using OpenTK.Mathematics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using static OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.Editors.DrawXGridHelper;
using Color = System.Drawing.Color;
using Vector4 = System.Numerics.Vector4;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

public partial class FumenVisualEditorViewModel : DocumentViewModelBase, ISchedulable, IFumenEditorDrawingContext
{
    private Dictionary<string, IFumenEditorDrawingTarget[]> drawTargetMap = new();
    private IPerfomenceMonitor actualPerformenceMonitor;

    private readonly List<CacheDrawXLineResult> cachedMagneticXGridLines = new();

    private Func<double, FumenVisualEditorViewModel, SoflanList, double>
        convertToY = (tUnit, editor, _) => TGridCalculator.ConvertTGridUnitToY_DesignMode(tUnit, editor);

    private string displayFPS = "";
    private readonly Dictionary<IFumenEditorDrawingTarget, Dictionary<DrawingTargetContext, List<OngekiObjectBase>>> drawMap = new();
    private IFumenEditorDrawingTarget[] drawTargetOrder;
    private readonly IPerfomenceMonitor dummyPerformenceMonitor = new DummyPerformenceMonitor();
    private bool enablePlayFieldDrawing;

    private bool isDisplayFPS;
    private DrawJudgeLineHelper judgeLineHelper;
    private DrawPlayableAreaHelper playableAreaHelper;
    internal GlobalCacheSoflanGroupRecorder _cacheSoflanGroupRecorder = new();
    private DrawHitObjectEffectHelper hitObjectEffectHelper;
    private DrawPlayerLocationHelper playerLocationHelper;
    private Vector4 playFieldBackgroundColor;

    private DrawSelectingRangeHelper selectingRangeHelper;

    private readonly StringBuilder stringBuilder = new(2048);

    private DrawTimeSignatureHelper timeSignatureHelper;

    private float viewHeight;
    private float viewWidth;

    private DrawXGridHelper xGridHelper;
    private int cacheMagaticXGridLinesHash;

    private IEnumerable<IFumenEditorDrawingTarget> drawingTargets = [];
    public IEnumerable<IFumenEditorDrawingTarget> CurrentDrawingTargets => drawingTargets;

    private readonly TaskCompletionSource renderInitializationTaskSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private VisibleRect rectInDesignMode;
    public VisibleRect RectInDesignMode
    {
        get => rectInDesignMode;
        set
        {
            SetProperty(ref rectInDesignMode, value);
            OnPropertyChanged();
        }
    }

    public IEnumerable<CacheDrawXLineResult> CachedMagneticXGridLines => cachedMagneticXGridLines;

    public PlayerLocationRecorder PlayerLocationRecorder { get; } = new();

    public bool IsDisplayFPS
    {
        get => isDisplayFPS;
        set
        {
            SetProperty(ref isDisplayFPS, value);
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
            OnPropertyChanged();
        }
    }

    public float ViewWidth
    {
        get => viewWidth;
        set
        {
            SetProperty(ref viewWidth, value);
        }
    }

    public float ViewHeight
    {
        get => viewHeight;
        set
        {
            SetProperty(ref viewHeight, value);
        }
    }

    public DrawingTargetContext CurrentDrawingTargetContext { get; set; }

    public TimeSpan CurrentPlayTime { get; private set; } = TimeSpan.FromSeconds(0);

    public FumenVisualEditorViewModel Editor => this;

    public IPerfomenceMonitor PerfomenceMonitor { get; private set; } = new DummyPerformenceMonitor();

    public void LoadRenderOrderVisible()
    {
        var targets = drawTargetMap.Values.SelectMany(x => x).Distinct().ToArray();
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
        var targets = drawTargetMap.Values.SelectMany(x => x).Distinct().ToArray();
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

    public void PrepareRenderLoop(FrameworkElement renderControl, IRenderManagerImpl renderImpl)
    {
        PrepareRenderLoop(
            renderControl,
            renderImpl,
            IoC.GetAll<IFumenEditorDrawingTarget>(),
            IoC.Get<IPerfomenceMonitor>());
    }

    internal void PrepareRenderLoop(
        FrameworkElement renderControl,
        IRenderManagerImpl renderImpl,
        IEnumerable<IFumenEditorDrawingTarget> availableDrawingTargets,
        IPerfomenceMonitor performenceMonitor)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ViewWidth = (float)renderControl.Bounds.Width;
        ViewHeight = (float)renderControl.Bounds.Height;

        playFieldBackgroundColor = Color.FromArgb(EditorGlobalSetting.Default.PlayFieldBackgroundColor).ToVector4();
        enablePlayFieldDrawing = EditorGlobalSetting.Default.EnablePlayFieldDrawing;
        hideWallLaneWhenEnablePlayField = EditorGlobalSetting.Default.HideWallLaneWhenEnablePlayField;

        //get and initialize drawing targets.
        drawingTargets = availableDrawingTargets.ToArray();
        foreach (var drawTarget in drawingTargets)
            drawTarget.Initialize(renderImpl);
        //build map for ongeki objects
        drawTargetMap = drawingTargets
            .SelectMany(target => target.DrawTargetID.Select(supportId => (supportId, target)))
            .GroupBy(x => x.supportId).ToDictionary(x => x.Key, x => x.Select(x => x.target).ToArray());

        LoadRenderOrderVisible();
        ResortRenderOrder();

        DisposeDrawingHelpers();
        timeSignatureHelper = new DrawTimeSignatureHelper();
        timeSignatureHelper.Initalize(renderImpl);

        xGridHelper = new DrawXGridHelper();
        xGridHelper.Initalize(renderImpl);

        judgeLineHelper = new DrawJudgeLineHelper();
        judgeLineHelper.Initalize(renderImpl);

        selectingRangeHelper = new DrawSelectingRangeHelper();
        selectingRangeHelper.Initalize(renderImpl);

        playableAreaHelper = new DrawPlayableAreaHelper();
        playableAreaHelper.Initalize(renderImpl);

        playerLocationHelper = new DrawPlayerLocationHelper();
        playerLocationHelper.Initalize(renderImpl);

        hitObjectEffectHelper = new DrawHitObjectEffectHelper();
        hitObjectEffectHelper.Initalize(renderImpl);

        actualPerformenceMonitor = performenceMonitor;
        IsDisplayFPS = IsDisplayFPS;

        UpdateActualRenderInterval();
        sw = new Stopwatch();
        sw.Start();

        renderInitializationTaskSource.TrySetResult();
    }

    private void OnEditorLoop(TimeSpan ts)
    {
        //todo update() not should be in render loop
        OnEditorUpdate(ts);

        OnEditorRender(ts);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Render(TimeSpan ts)
        => OnEditorLoop(ts);

    private readonly ConcurrentDictionary<int, DrawingTargetContext> drawingContexts = new();
    private IRenderManagerImpl renderImpl;
    private ContentControl renderControlHost;
    private FrameworkElement attachedRenderControl;
    private CancellationTokenSource renderControlLifetimeCancellationSource;
    private CancellationToken renderControlLifetimeCancellationToken = new(canceled: true);
    private int renderControlAttachmentVersion;
    private bool isRenderControlLoaded;

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
        #region limit fps

        if (actualRenderInterval > 0)
        {
            var ms = sw.ElapsedMilliseconds;
            if (ms < actualRenderInterval)
                goto End;
            ts = TimeSpan.FromMilliseconds(ms);
            sw.Restart();
        }

        #endregion

        #region clean and prepare perfomence statistics

        PerfomenceMonitor.PostUIRenderTime(ts);
        PerfomenceMonitor.OnBeforeRender();

        ClearHitObjects();

        drawingContexts.Clear();

        #endregion

        var fumen = EditorContext.Fumen;
        if (fumen is null)
            goto End;

        //计算可以显示的TGrid范围以及像素范围

        var tGrid = GetCurrentTGrid();

        #region prepare drawing contexts' for every soflan groups 

        var projectionMatrix =
            Matrix4.CreateOrthographic(ViewWidth, ViewHeight, -1, 1);

        IEnumerable<KeyValuePair<int, SoflanList>> soflanMap = EditorContext.Fumen.SoflansMap;
        //if (IsDesignMode)
        //    soflanMap = [new KeyValuePair<int, SoflanList>(0, EditorContext.Fumen.SoflansMap.DefaultSoflanList)];

        foreach (KeyValuePair<int, SoflanList> pair in soflanMap)
        {
            var curY = ConvertToY(tGrid.TotalUnit, pair.Value);
            var minY = (float)(curY - Setting.JudgeLineOffsetY);
            var maxY = minY + ViewHeight;

            var visibleTGridRanges = new SortableCollection<(TGrid minTGrid, TGrid maxTGrid), TGrid>(x => x.minTGrid);

            if (IsPreviewMode)
            {
                //Preview Mode
                var ranges =
                    pair.Value.GetVisibleRanges_PreviewMode(curY, ViewHeight, Setting.JudgeLineOffsetY, EditorContext.Fumen.BpmList,
                        Setting.VerticalDisplayScale);
                foreach (var x in ranges)
                {
                    if (x.maxTGrid is null || x.minTGrid is null)
                        continue;
                    visibleTGridRanges.Add((x.minTGrid, x.maxTGrid));
                }
            }
            else
            {
                //Design Mode
                var minTGrid = TGridCalculator.ConvertYToTGrid_DesignMode(minY, this) ?? TGrid.Zero;
                var maxTGrid = TGridCalculator.ConvertYToTGrid_DesignMode(maxY, this) ?? TGrid.Zero;
                visibleTGridRanges.Add((minTGrid, maxTGrid));
            }

            var rect = new VisibleRect(new Vector2(ViewWidth, minY), new Vector2(0, minY + ViewHeight));

            var y = (float)curY;
            var viewMatrix = Matrix4.CreateTranslation(new Vector3(-ViewWidth / 2,
                -y - ViewHeight / 2 + (float)Setting.JudgeLineOffsetY, 0));

            var drawingContext = new DrawingTargetContext()
            {
                CurrentSoflanList = pair.Value,
                VisibleTGridRanges = visibleTGridRanges,
                SoflanGroupId = pair.Key,
                Rect = rect,
                ViewMatrix = viewMatrix,
                ProjectionMatrix = projectionMatrix,
                ViewWidth = ViewWidth,
                ViewHeight = ViewHeight
            };

            drawingContexts[pair.Key] = drawingContext;
        }

        var defaultDrawingTargetContext = drawingContexts[0];

        if (IsDesignMode)
            RectInDesignMode = defaultDrawingTargetContext.Rect;

        #endregion

        // objType -> soflanGroup -> obj[]
        var map = ObjectPool<Dictionary<string, Dictionary<DrawingTargetContext, List<OngekiTimelineObjectBase>>>>.Get();
        map.Clear();

        var usedDrawingContexts = ObjectPool<HashSet<int>>.Get();
        usedDrawingContexts.Clear();
        //always draw default soflan group
        usedDrawingContexts.Add(0);

        //Prepare objects we will draw them.
        //get&register all visible objects for every drawingContext(soflanGroup)
        var allVisibleTGridRanges = drawingContexts.Values.SelectMany(x => x.VisibleTGridRanges).Merge();
        var visibleObjects = EnumerateAllDisplayableObjects(fumen, allVisibleTGridRanges).OfType<OngekiTimelineObjectBase>();
        foreach (var obj in visibleObjects)
        {
            if (!map.TryGetValue(obj.IDShortName, out var soflanGroupObjectMap))
            {
                soflanGroupObjectMap = map[obj.IDShortName] = ObjectPool<Dictionary<DrawingTargetContext, List<OngekiTimelineObjectBase>>>.Get();
                soflanGroupObjectMap.Clear();
            }

            _cacheSoflanGroupRecorder.GetCache(obj.Id, out var soflanGroup);

            if (!CheckSoflanGroupVisible(soflanGroup))
                continue;

            if (drawingContexts.TryGetValue(soflanGroup, out var drawingContext))
            {
                if (!soflanGroupObjectMap.TryGetValue(drawingContext, out var list))
                {
                    list = soflanGroupObjectMap[drawingContext] = ObjectPool<List<OngekiTimelineObjectBase>>.Get();
                    list.Clear();
                }

                list.Add(obj);
                usedDrawingContexts.Add(soflanGroup);
            }
            else
            {
                //todo log it
            }
        }

        foreach (var objGroup in map)
        {
            if (GetDrawingTarget(objGroup.Key) is not IFumenEditorDrawingTarget[] drawingTargets)
                continue;
            var soflanGroupObjectMap = objGroup.Value;

            foreach (var drawingTarget in drawingTargets)
            {
                if (!drawMap.TryGetValue(drawingTarget, out var enums))
                {
                    var r = drawMap[drawingTarget] = ObjectPool<Dictionary<DrawingTargetContext, List<OngekiObjectBase>>>.Get();
                    r.Clear();
                    foreach (var pair in soflanGroupObjectMap)
                    {
                        var rr = r[pair.Key] = ObjectPool<List<OngekiObjectBase>>.Get();
                        rr.Clear();
                        rr.AddRange(pair.Value);
                    }
                }
                else
                {
                    foreach (var pair in soflanGroupObjectMap)
                    {
                        if (!enums.TryGetValue(pair.Key, out var rr))
                        {
                            rr = enums[pair.Key] = ObjectPool<List<OngekiObjectBase>>.Get();
                            rr.Clear();
                        }

                        rr.AddRange(pair.Value);
                    }
                }
            }
        }

        //remove unused drawingContexts
        var unusedSoflanGroups = ObjectPool.Get<List<int>>();
        unusedSoflanGroups.Clear();
        unusedSoflanGroups.AddRange(drawingContexts.Keys.Except(usedDrawingContexts));
        foreach (var soflanGroupId in unusedSoflanGroups)
            drawingContexts.TryRemove(soflanGroupId, out _);

        RecalculateMagaticXGridLines();

        if (IsPreviewMode)
        {
            /*
            (DrawingTargetContext ctx, OngekiTimelineObjectBase obj) Convert(OngekiTimelineObjectBase obj)
            {
                _cacheSoflanGroupRecorder.GetCache(obj, out var soflanGroup);
                var drawingContext = drawingContexts.TryGetValue(soflanGroup, out var ctx) ? ctx : drawingContexts[0];
                return (drawingContext, obj);
            }
            */

            //特殊处理：子弹和Bell
            var blts = EditorContext.Fumen.Bullets.AsEnumerable();
            var bels = EditorContext.Fumen.Bells.AsEnumerable();
            var curTGrid = GetCurrentTGrid();
            if (IsPreviewMode)
            {
                blts = EditorContext.Fumen.Bullets.BinaryFindRange(curTGrid, TGrid.MaxValue);
                bels = EditorContext.Fumen.Bells.BinaryFindRange(curTGrid, TGrid.MaxValue);
            }
            bels = bels.Where(x =>
            {
                _cacheSoflanGroupRecorder.GetCache(x, out var soflanGroup);
                return CheckSoflanGroupVisible(soflanGroup);
            });
            blts = blts.Where(x =>
            {
                _cacheSoflanGroupRecorder.GetCache(x, out var soflanGroup);
                return CheckSoflanGroupVisible(soflanGroup);
            });

            foreach (var drawingTarget in GetDrawingTarget(Bullet.CommandName))
            {
                //todo 优化一下
                var r = drawMap[drawingTarget] = ObjectPool<Dictionary<DrawingTargetContext, List<OngekiObjectBase>>>.Get();
                r.Clear();
                var rr = r[defaultDrawingTargetContext] = ObjectPool<List<OngekiObjectBase>>.Get();
                rr.Clear();
                rr.AddRange(blts);
            }
            foreach (var drawingTarget in GetDrawingTarget(Bell.CommandName))
            {
                //todo 优化一下
                var r = drawMap[drawingTarget] = ObjectPool<Dictionary<DrawingTargetContext, List<OngekiObjectBase>>>.Get();
                r.Clear();
                var rr = r[defaultDrawingTargetContext] = ObjectPool<List<OngekiObjectBase>>.Get();
                rr.Clear();
                rr.AddRange(bels);
            }
        }

        #region Rendering

        CurrentDrawingTargetContext = defaultDrawingTargetContext;

        CleanRender();
        RenderContext?.BeforeRender(this);

        foreach (var (minTGrid, maxTGrid) in CurrentDrawingTargetContext.VisibleTGridRanges)
            playableAreaHelper.DrawPlayField(this, minTGrid, maxTGrid);

        playableAreaHelper.Draw(this);
        timeSignatureHelper.DrawLines(this);

        xGridHelper.DrawLines(this, CachedMagneticXGridLines);

        var prevOrder = int.MinValue;
        foreach (var drawingTarget in drawTargetOrder.Where(x => CheckDrawingVisible(x.Visible)))
        {
            //check render order
            var order = drawingTarget.CurrentRenderOrder;
            if (prevOrder > order)
            {
                ResortRenderOrder();
                break;
            }

            prevOrder = order;

            CurrentDrawingTargetContext = defaultDrawingTargetContext;

            PerfomenceMonitor.OnBeginTargetDrawing(drawingTarget);
            {
                if (drawMap.TryGetValue(drawingTarget, out var drawingObjs))
                {
                    foreach (var soflanGroupDrawing in drawingObjs)
                    {
                        CurrentDrawingTargetContext = soflanGroupDrawing.Key;

                        drawingTarget.Begin(this);
                        //all object collection has been sorted within GetDisplayableObjects()
                        foreach (var obj in soflanGroupDrawing.Value/*.OrderBy(x => x.TGrid)*/)
                            drawingTarget.Post(obj);
                        drawingTarget.End();
                    }
                }
            }
            PerfomenceMonitor.OnAfterTargetDrawing(drawingTarget);
        }

        CurrentDrawingTargetContext = defaultDrawingTargetContext;

        timeSignatureHelper.DrawTimeSigntureText(this);
        xGridHelper.DrawXGridText(this, CachedMagneticXGridLines);
        judgeLineHelper.Draw(this);
        hitObjectEffectHelper.Draw(this);
        playerLocationHelper.Draw(this);
        selectingRangeHelper.Draw(this);

        RenderContext?.AfterRender(this);

        //clean up
        foreach (var list in map.Values)
        {
            foreach (var item in list)
                ObjectPool.Return(item.Value);
            ObjectPool.Return(list);
        }

        foreach (var list in drawMap.Values)
        {
            foreach (var item in list)
                ObjectPool.Return(item.Value);
            ObjectPool.Return(list);
        }

        ObjectPool.Return(map);
        ObjectPool.Return(usedDrawingContexts);
        ObjectPool.Return(unusedSoflanGroups);

        #endregion
    End:
        drawMap.Clear();
        PerfomenceMonitor.OnAfterRender();
        //set null
        CurrentDrawingTargetContext = default;
    }

    public bool CheckDrawingVisible(DrawingVisible visible)
    {
        return visible.HasFlag(EditorObjectVisibility
            ? DrawingVisible.Design
            : DrawingVisible.Preview);
    }

    public double ConvertToY(double tGridUnit, SoflanList soflanList)
    {
        return convertToY(tGridUnit, this, soflanList);
    }

    public bool CheckVisible(TGrid tGrid)
    {
        foreach (var ctx in drawingContexts.Values)
        {
            if (CheckVisible(ctx, tGrid))
                return true;
        }

        return false;
    }

    public bool CheckRangeVisible(TGrid minTGrid, TGrid maxTGrid)
    {
        foreach (var ctx in drawingContexts.Values)
        {
            if (CheckRangeVisible(ctx, minTGrid, maxTGrid))
                return true;
        }

        return false;
    }

    public string SchedulerName => "Fumen Previewer Performance Statictis";

    public TimeSpan ScheduleCallLoopInterval => TimeSpan.FromSeconds(1);

    public IRenderContext RenderContext { get; private set; }

    public void OnSchedulerTerm()
    {
    }

    public async Task OnScheduleCall(CancellationToken cancellationToken)
    {
        if (IsDisplayFPS)
        {
            stringBuilder.Clear();

            PerfomenceMonitor?.FormatStatistics(stringBuilder);
#if DEBUG
            var drawingContextSnapshot = drawingContexts.ToArray();
            stringBuilder.AppendLine();
            stringBuilder.AppendLine($"Viewport: {ViewWidth}x{ViewHeight}");
            stringBuilder.AppendLine($"VisibleRanges ({drawingContextSnapshot.Length} sfl groups):");

            foreach (var item in drawingContextSnapshot.OrderBy(x => x.Key))
            {
                var ranges = item.Value?.VisibleTGridRanges;
                if (ranges != null)
                {
                    foreach (var tGridRange in ranges)
                        stringBuilder.AppendLine($"*[{item.Key}]  {tGridRange.minTGrid}  -  {tGridRange.maxTGrid} -> {item.Value.Rect.MinY:F2} -  {item.Value.Rect.MaxY:F2}");

                }
            }

            if (IsPreviewMode)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    stringBuilder.AppendLine();
                    var defaultDrawingContext = drawingContextSnapshot.FirstOrDefault(x => x.Key == 0).Value;
                    if (defaultDrawingContext?.Rect.MaxY - lastPointerViewPosition.Y is double mouseY)
                    {
                        stringBuilder.AppendLine($"MouseY: {mouseY:F2}");
                        foreach (var tGrid in TGridCalculator.ConvertYToTGrid_PreviewMode(mouseY, this))
                            stringBuilder.AppendLine($"* {tGrid}");
                    }
                });
            }
#endif

            var displayText = stringBuilder.ToString();
            // 调度器在后台线程运行，Avalonia 不会自动封送 INPC，回 UI 线程赋值。
            Dispatcher.UIThread.Post(() => DisplayFPS = displayText);

            PerfomenceMonitor?.Clear();
        }
    }

    public override void OnViewAfterLoaded(IView view)
    {
        base.OnViewAfterLoaded(view);
        AttachRuntimeSubscriptions();
        View = view as FumenVisualEditorView;
        UpdateBatchModeBehaviorAttachment();
        InitExtraMenuItems();
    }

    public override void OnViewBeforeUnload(IView view)
    {
        DetachBatchModeBehavior();
        DetachRuntimeSubscriptions();
        View = null;
        base.OnViewBeforeUnload(view);
    }

    private void ResortRenderOrder()
    {
        drawTargetOrder = drawTargetMap.Values.SelectMany(x => x).OrderBy(x => x.CurrentRenderOrder).Distinct().ToArray();
    }

    public IFumenEditorDrawingTarget[] GetDrawingTarget(string name)
    {
        return drawTargetMap.TryGetValue(name, out var drawingTarget) ? drawingTarget : default;
    }

    private IEnumerable<IDisplayableObject> EnumerateAllDisplayableObjects(OngekiFumen fumen,
        IEnumerable<(TGrid min, TGrid max)> visibleRanges)
    {
        var containBeams = fumen.Beams.Any();
        var judgeTGrid = GetCurrentTGrid();

        var objects = visibleRanges.SelectMany(x =>
        {
            var (min, max) = x;

            var playableObjects = Enumerable.Empty<OngekiMovableObjectBase>()
            .Concat(fumen.Flicks.BinaryFindRange(min, max))
            .Concat(fumen.Taps.BinaryFindRange(min, max));

            var playableDurationObjects = fumen.Holds.GetVisibleStartObjects(min, max);

            if (IsPreviewMode)
            {
                playableObjects = playableObjects.Where(x => x.TGrid > judgeTGrid);
                playableDurationObjects = playableDurationObjects.Where(x => x.EndTGrid > judgeTGrid);
            }

            var r = Enumerable.Empty<IDisplayableObject>()
                .Concat(fumen.MeterChanges.Skip(1)) //not show first meter
                .Concat(fumen.BpmList.Skip(1)) //not show first bpm
                .Concat(fumen.ClickSEs.BinaryFindRange(min, max))
                .Concat(fumen.LaneBlocks.GetVisibleStartObjects(min, max))
                .Concat(fumen.Comments.BinaryFindRange(min, max))
                .Concat(fumen.SoflansMap.Values.SelectMany(x => x.GetVisibleStartObjects(min, max)))
                .Concat(fumen.IndividualSoflanAreaMap.Values.SelectMany(x => x.GetVisibleStartObjects(min, max)))
                .Concat(fumen.EnemySets.BinaryFindRange(min, max))
                // SVG prefabs are temporarily excluded from the editor drawing/object-selection pipeline.
                // .Concat(fumen.SvgPrefabs.BinaryFindRange(min, max))
                .Concat(fumen.Lanes.GetVisibleStartObjects(min, max))
                .Concat(playableDurationObjects)
                .Concat(playableObjects);

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
        var cleanColor = Vector4.Zero;
        if (IsDesignMode || !enablePlayFieldDrawing)
            cleanColor = new(16 / 255.0f, 16 / 255.0f, 16 / 255.0f, 1);
        else
        {
            cleanColor = new(playFieldBackgroundColor.X, playFieldBackgroundColor.Y, playFieldBackgroundColor.Z,
                playFieldBackgroundColor.W);
#if PLAYFIELD_DEBUG
            cleanColor = new(0, 0, 0, 1);
#endif
        }
        RenderContext?.CleanRender(this, cleanColor);
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
        var scrollViewer = e.Source as ScrollViewer;
        scrollViewer?.InvalidateMeasure();
    }

    public bool CheckSoflanGroupVisible(int soflanGroup)
    {
        var soflanGroupWrapItem = EditorContext.Fumen.IndividualSoflanAreaMap.TryGetOrCreateSoflanGroupWrapItem(soflanGroup, out _);
        if (IsDesignMode)
        {
            return soflanGroupWrapItem.IsDisplayInDesignMode;
        }
        else
        {
            return soflanGroupWrapItem.IsDisplayInPreviewMode;
        }
    }

    public bool CheckVisible(DrawingTargetContext context, TGrid tGrid)
    {
        foreach (var (minTGrid, maxTGrid) in context.VisibleTGridRanges)
            if (minTGrid <= tGrid && tGrid <= maxTGrid)
                return true;
        return false;
    }

    public bool CheckRangeVisible(DrawingTargetContext context, TGrid minTGrid, TGrid maxTGrid)
    {
        foreach (var visibleRange in drawingContexts.SelectMany(x => x.Value.VisibleTGridRanges))
        {
            var result = !(minTGrid > visibleRange.maxTGrid || maxTGrid < visibleRange.minTGrid);
            if (result)
                return true;
        }

        return false;
    }

    private ActionExecutionContext CreateExecutionContext(object source, object eventArgs)
        => new() { Source = source, EventArgs = eventArgs, View = View };

    public Task InitializeRenderControlAsync(ContentControl contentControl)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return InitializeRenderControlAsync(
            contentControl,
            IoC.Get<IRenderManager>().GetCurrentRenderManagerImpl(),
            IoC.GetAll<IFumenEditorDrawingTarget>(),
            IoC.Get<IPerfomenceMonitor>());
    }

    internal async Task InitializeRenderControlAsync(
        ContentControl contentControl,
        IRenderManagerImpl newRenderImpl,
        IEnumerable<IFumenEditorDrawingTarget> availableDrawingTargets,
        IPerfomenceMonitor performenceMonitor,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(contentControl);
        ArgumentNullException.ThrowIfNull(newRenderImpl);
        ArgumentNullException.ThrowIfNull(availableDrawingTargets);
        ArgumentNullException.ThrowIfNull(performenceMonitor);

        if (attachedRenderControl is not null)
            return;

        var attachmentVersion = Interlocked.Increment(ref renderControlAttachmentVersion);
        var lifetimeCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var lifetimeCancellationToken = lifetimeCancellationSource.Token;
        renderControlLifetimeCancellationSource = lifetimeCancellationSource;
        renderControlLifetimeCancellationToken = lifetimeCancellationToken;
        renderImpl = newRenderImpl;
        renderControlHost = contentControl;
        var newRenderControl = newRenderImpl.CreateRenderControl();
        attachedRenderControl = newRenderControl;

        try
        {
            await newRenderImpl.InitializeRenderControl(newRenderControl, lifetimeCancellationToken);
            lifetimeCancellationToken.ThrowIfCancellationRequested();
            if (attachmentVersion != Volatile.Read(ref renderControlAttachmentVersion) || IsDisposed)
                return;

            Log.LogDebug($"RenderControl({newRenderControl.GetHashCode()}) is created");
            AttachRenderControlHandlers(newRenderControl);
            contentControl.Content = newRenderControl;
            PrepareRenderLoop(newRenderControl, newRenderImpl, availableDrawingTargets, performenceMonitor);
        }
        catch (OperationCanceledException) when (
            lifetimeCancellationToken.IsCancellationRequested ||
            IsDisposed ||
            attachmentVersion != Volatile.Read(ref renderControlAttachmentVersion))
        {
            if (attachmentVersion == Volatile.Read(ref renderControlAttachmentVersion))
                DetachRenderControl();
        }
        catch (Exception exception)
        {
            if (attachmentVersion == Volatile.Read(ref renderControlAttachmentVersion))
            {
                DetachRenderControl();
                DisposeDrawingHelpers();
                renderInitializationTaskSource.TrySetException(exception);
            }

            throw;
        }
    }

    private void AttachRenderControlHandlers(FrameworkElement renderControl)
    {
        renderControl.Loaded += RenderControl_Loaded;
        renderControl.Unloaded += RenderControl_UnLoaded;
        renderControl.SizeChanged += RenderControl_SizeChanged;
        renderControl.PointerWheelChanged += RenderControl_PointerWheelChanged;
        renderControl.PointerPressed += RenderControl_PointerPressed;
        renderControl.PointerMoved += RenderControl_PointerMoved;
        renderControl.PointerReleased += RenderControl_PointerReleased;
        renderControl.PointerExited += RenderControl_PointerExited;
    }

    private void DetachRenderControlHandlers(FrameworkElement renderControl)
    {
        renderControl.Loaded -= RenderControl_Loaded;
        renderControl.Unloaded -= RenderControl_UnLoaded;
        renderControl.SizeChanged -= RenderControl_SizeChanged;
        renderControl.PointerWheelChanged -= RenderControl_PointerWheelChanged;
        renderControl.PointerPressed -= RenderControl_PointerPressed;
        renderControl.PointerMoved -= RenderControl_PointerMoved;
        renderControl.PointerReleased -= RenderControl_PointerReleased;
        renderControl.PointerExited -= RenderControl_PointerExited;
    }

    private void RenderControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Log.LogDebug($"renderControl new size: {e.NewSize}");

        ViewWidth = (float)e.NewSize.Width;
        ViewHeight = (float)e.NewSize.Height;
        RecalculateTotalDurationHeight();
        OnSizeChanged(CreateExecutionContext(sender, e));
    }

    private void RenderControl_PointerWheelChanged(object sender, PointerWheelEventArgs e) =>
        OnMouseWheel(CreateExecutionContext(sender, e));

    private void RenderControl_PointerPressed(object sender, PointerPressedEventArgs e) =>
        OnMouseDown(CreateExecutionContext(sender, e));

    private void RenderControl_PointerMoved(object sender, PointerEventArgs e) =>
        OnMouseMove(CreateExecutionContext(sender, e));

    private void RenderControl_PointerReleased(object sender, PointerReleasedEventArgs e) =>
        OnMouseUp(CreateExecutionContext(sender, e));

    private void RenderControl_PointerExited(object sender, PointerEventArgs e) =>
        OnMouseLeave(CreateExecutionContext(sender, e));

    private void RenderControl_UnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement renderControl || !ReferenceEquals(renderControl, attachedRenderControl))
            return;

        Log.LogDebug($"RenderControl({renderControl.GetHashCode()}) is unloaded");
        isRenderControlLoaded = false;
        StopRenderContext();
    }

    private async void RenderControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement renderControl)
            return;

        await ActivateRenderControlAsync(renderControl, e);
    }

    internal async Task ActivateRenderControlAsync(FrameworkElement renderControl, object eventArgs)
    {
        if (!ReferenceEquals(renderControl, attachedRenderControl) || IsDisposed)
            return;

        Log.LogDebug($"RenderControl({renderControl.GetHashCode()}) is loaded");
        isRenderControlLoaded = true;
        var attachmentVersion = Volatile.Read(ref renderControlAttachmentVersion);
        var lifetimeCancellationToken = renderControlLifetimeCancellationToken;
        var currentRenderImpl = renderImpl;
        if (lifetimeCancellationToken.IsCancellationRequested || currentRenderImpl is null)
            return;

        try
        {
            var renderContext = await currentRenderImpl.GetRenderContext(
                renderControl,
                lifetimeCancellationToken);
            if (IsDisposed ||
                !isRenderControlLoaded ||
                attachmentVersion != Volatile.Read(ref renderControlAttachmentVersion) ||
                !ReferenceEquals(renderControl, attachedRenderControl))
                return;

            StartRenderContext(renderContext);
            OnLoaded(CreateExecutionContext(renderControl, eventArgs));
        }
        catch (OperationCanceledException) when (IsDisposed || lifetimeCancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Log.LogError($"Unable to start render control {renderControl.GetHashCode()}: {exception.Message}", exception);
        }
    }

    private void StartRenderContext(IRenderContext renderContext)
    {
        if (!ReferenceEquals(RenderContext, renderContext))
            StopRenderContext();

        RenderContext = renderContext;
        renderContext.OnRender -= Render;
        renderContext.OnRender += Render;
        renderContext.StartRendering();
    }

    private void StopRenderContext()
    {
        var renderContext = RenderContext;
        RenderContext = null;
        if (renderContext is null)
            return;

        renderContext.OnRender -= Render;
        renderContext.StopRendering();
    }

    private void DetachRenderControl()
    {
        Interlocked.Increment(ref renderControlAttachmentVersion);
        isRenderControlLoaded = false;

        var lifetimeCancellationSource = renderControlLifetimeCancellationSource;
        renderControlLifetimeCancellationSource = null;
        renderControlLifetimeCancellationToken = new(canceled: true);
        lifetimeCancellationSource?.Cancel();
        lifetimeCancellationSource?.Dispose();

        StopRenderContext();

        var renderControl = attachedRenderControl;
        attachedRenderControl = null;
        if (renderControl is not null)
            DetachRenderControlHandlers(renderControl);

        var contentControl = renderControlHost;
        renderControlHost = null;
        if (contentControl is not null && ReferenceEquals(contentControl.Content, renderControl))
            contentControl.Content = null;

        var currentRenderImpl = renderImpl;
        renderImpl = null;
        if (renderControl is not null)
            currentRenderImpl?.ReleaseRenderControl(renderControl);
    }

    private void DisposeDrawingHelpers()
    {
        playableAreaHelper?.Dispose();
        playableAreaHelper = null;
        playerLocationHelper?.Dispose();
        playerLocationHelper = null;
        hitObjectEffectHelper?.Dispose();
        hitObjectEffectHelper = null;

        timeSignatureHelper = null;
        xGridHelper = null;
        judgeLineHelper = null;
        selectingRangeHelper = null;
    }

    private void DisposeRenderResources()
    {
        DetachRenderControl();
        DisposeDrawingHelpers();

        drawingTargets = [];
        drawTargetOrder = [];
        drawTargetMap.Clear();
        drawMap.Clear();
        drawingContexts.Clear();
        cachedMagneticXGridLines.Clear();
        CurrentDrawingTargetContext = null;
        actualPerformenceMonitor = null;
        PerfomenceMonitor = dummyPerformenceMonitor;
        sw?.Stop();
        sw = null;
        renderInitializationTaskSource.TrySetResult();
    }

    public Task WaitForRenderInitializationIsDone()
    {
        return renderInitializationTaskSource.Task;
    }
}



