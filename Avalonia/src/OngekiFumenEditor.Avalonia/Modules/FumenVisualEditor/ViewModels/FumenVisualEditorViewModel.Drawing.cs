using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Views;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Platforms.Services.Window;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.Collections.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.Editors;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.ObjectPool;
using OpenTK.Mathematics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.Editors.DrawXGridHelper;
using Color = System.Drawing.Color;
using Vector4 = System.Numerics.Vector4;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

public partial class FumenVisualEditorViewModel : DocumentViewModelBase, IFumenEditorDrawingContext
{
    private Dictionary<string, IFumenEditorDrawingTarget[]> drawTargetMap = new();

    private readonly List<CacheDrawXLineResult> cachedMagneticXGridLines = new();

    private Func<double, FumenVisualEditorViewModel, SoflanList, double>
        convertToY = (tUnit, editor, _) => TGridCalculator.ConvertTGridUnitToY_DesignMode(tUnit, editor);

    private readonly Dictionary<IFumenEditorDrawingTarget, IPooledDictionary<DrawingTargetContext, IPooledList<OngekiObjectBase>>> drawMap = new();
    private IFumenEditorDrawingTarget[] drawTargetOrder;
    private bool enablePlayFieldDrawing;

    private DrawJudgeLineHelper judgeLineHelper;
    private DrawPlayableAreaHelper playableAreaHelper;
    internal GlobalCacheSoflanGroupRecorder _cacheSoflanGroupRecorder = new();
    private DrawHitObjectEffectHelper hitObjectEffectHelper;
    private DrawPlayerLocationHelper playerLocationHelper;
    private Vector4 playFieldBackgroundColor;

    private DrawSelectingRangeHelper selectingRangeHelper;


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


    private Stopwatch sw;
    private float actualRenderInterval;


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
            if (SetProperty(ref viewHeight, value))
                OnPropertyChanged(nameof(IsVerticalScrollBarVisible));
        }
    }

    public DrawingTargetContext CurrentDrawingTargetContext { get; set; }

    public TimeSpan CurrentPlayTime { get; private set; } = TimeSpan.FromSeconds(0);

    public FumenVisualEditorViewModel Editor => this;

    public IPerfomenceMonitor PerfomenceMonitor => RenderContext?.PerfomenceMonitor ?? DummyPerformenceMonitor.Instance;

    [RelayCommand]
    private Task OpenRenderPerfomenceMeasurePanelAsync() =>
        IoC.Get<IWindowManager>().ShowWindowAsync(IoC.Get<RenderPerfomenceMeasurePanelViewModel>());

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
                Log.LogError($"load json content failed:{e.Message}, drawing targets will use default configs.", e);
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
            Log.LogError($"save json content failed:{e.Message}", e);
            map = new();
        }
    }

    public void PrepareRenderLoop(FrameworkElement renderControl, IRenderManagerImpl renderImpl)
    {
        PrepareRenderLoop(renderControl, renderImpl, IoC.GetAll<IFumenEditorDrawingTarget>());
    }

    internal void PrepareRenderLoop(
        FrameworkElement renderControl,
        IRenderManagerImpl renderImpl,
        IEnumerable<IFumenEditorDrawingTarget> availableDrawingTargets)
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

        UpdateActualRenderInterval();
        sw = new Stopwatch();
        sw.Start();

        renderInitializationTaskSource.TrySetResult();
    }

    private void OnEditorLoop(TimeSpan ts)
    {
        // The render context invokes this command-building callback on the UI thread.
        OnEditorUpdate(ts);

        OnEditorRender(ts);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Render(TimeSpan ts)
        => OnEditorLoop(ts);

    private void OnRenderFrame(IRenderContext renderContext, TimeSpan ts)
        => OnEditorLoop(ts);

    /// <summary>
    /// 本帧各 soflan 组的绘制上下文。
    ///
    /// 历史：258c32003 曾把它从 Dictionary 换成 ConcurrentDictionary，理由是
    /// 「渲染跑在 compositor 回调、指针输入跑在 UI 线程」。该前提在当前基线上已不成立：
    /// 渲染回调与指针输入同在 UI 线程（见 <see cref="OnEditorLoop"/> 的说明，以及
    /// UserInteractionActions.UpdateCurrentCursorPosition 的调用链），而真正跨线程的命中表
    /// 后来已改为「帧末发布不可变快照」（ClearHitObjects/CommitHitObjects/QueryHitObjects）。
    /// 故这里回退为普通 <see cref="Dictionary{TKey, TValue}"/>，写入点全部位于渲染线程。
    /// </summary>
    private readonly Dictionary<int, DrawingTargetContext> drawingContexts = new();

    /// <summary>
    /// 本帧「所有组可见 TGrid 区间」的合并结果（帧首计算一次）。
    /// <see cref="CheckRangeVisible(TGrid, TGrid)"/> 会被逐个可见子物体调用，
    /// 不能每次都去遍历 drawingContexts 现算，故这里缓存一份。
    /// </summary>
    private readonly List<(TGrid minTGrid, TGrid maxTGrid)> mergedVisibleTGridRanges = new();
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

        // 限帧闸门必须放在创建 builder **之前**：被丢弃的帧既不会生成任何绘制命令，
        // 也不需要 builder，提前创建只是白付 2 个池化字典 + 4 个池化列表的租借，
        // 以及 DefaultSkiaStringDrawing 里 3 个原生 SKPaint/SKFont 的构造与析构。
        // 声明先于闸门是刻意的：goto End 会跳过下面的赋值，End: 处的 builder?.Dispose()
        // 依赖它此时为 null（被丢弃的帧没有任何 builder 需要释放）。
        IDrawCommandListBuilder builder = null;

        if (actualRenderInterval > 0)
        {
            var ms = sw.ElapsedMilliseconds;
            if (ms < actualRenderInterval)
                goto End;
            ts = TimeSpan.FromMilliseconds(ms);
            sw.Restart();
        }

        builder = renderImpl?.CreateDrawCommandListBuilder();

        #endregion

        #region clean and prepare perfomence statistics


        ClearHitObjects();

        drawingContexts.Clear();
        mergedVisibleTGridRanges.Clear();

        #endregion

        if (builder is not null)
        {
            builder.SetCleanColor(GetCleanColor());
            builder.SetViewport(ViewWidth, ViewHeight);
            builder.SetCurrentViewMatrix(Matrix4.Identity);
            builder.SetCurrentProjectionMatrix(Matrix4.CreateOrthographic(ViewWidth, ViewHeight, -1, 1));
        }

        var fumen = EditorContext.Fumen;
        if (fumen is null)
        {
            // 没有谱面时命中表必须清空（与旧行为一致）：缓冲已在上面 Clear 过，这里直接发布空快照
            CommitHitObjects();
            goto End;
        }

        //计算可以显示的TGrid范围以及像素范围

        // 帧首唯一一次读取播放时间：整帧（原点、裁判线、特效、拍线……）都基于同一快照，
        // 命令生成和后续合成线程回放共享这一帧的时间基准。
        var frameTime = CurrentPlayTime;
        var frameTGrid = TGridCalculator.ConvertAudioTimeToTGrid(frameTime, this);

        var tGrid = frameTGrid;
        var editorOffsetMs = EditorGlobalSetting.Default.EditorOffsetMs;
        if (editorOffsetMs != 0)
        {
            var adjustedViewportAudioTime = frameTime + TimeSpan.FromMilliseconds(editorOffsetMs);
            if (adjustedViewportAudioTime < TimeSpan.Zero)
                adjustedViewportAudioTime = TimeSpan.Zero;
            tGrid = TGridCalculator.ConvertAudioTimeToTGrid(adjustedViewportAudioTime, fumen.BpmList);
        }

        tGrid ??= TGrid.Zero;
        frameTGrid ??= TGrid.Zero;

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
                using var ranges =
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

            //world rect: minY/maxY follow the scroll position (camera origin)
            var worldRect = new VisibleRect(new Vector2(ViewWidth, minY), new Vector2(0, maxY));
            //view-relative rect: fixed viewport, independent of scroll position
            var viewRelativeRect = new VisibleRect(new Vector2(ViewWidth, 0), new Vector2(0, ViewHeight));

            //de-globalize the view matrix: vertices carry view-relative Y already,
            //so the matrix must not fold the huge scroll offset in (catastrophic cancellation fix)
            var viewMatrix = Matrix4.CreateTranslation(new Vector3(-ViewWidth / 2, -ViewHeight / 2, 0));

            var drawingContext = new DrawingTargetContext()
            {
                CurrentSoflanList = pair.Value,
                VisibleTGridRanges = visibleTGridRanges,
                SoflanGroupId = pair.Key,
                ViewRelativeRect = viewRelativeRect,
                WorldRect = worldRect,
                ViewRelativeOriginY = minY,
                CurrentTime = frameTime,
                CurrentTGrid = frameTGrid,
                ViewMatrix = viewMatrix,
                ProjectionMatrix = projectionMatrix,
                ViewWidth = ViewWidth,
                ViewHeight = ViewHeight
            };

            drawingContexts[pair.Key] = drawingContext;
        }

        var defaultDrawingTargetContext = drawingContexts[0];

        if (IsDesignMode)
            RectInDesignMode = defaultDrawingTargetContext.WorldRect;

        #endregion

        // objType -> soflanGroup -> obj[]
        var drawingCollectionDisposables = ObjectPool.GetPooledList<IDisposable>();
        var map = ObjectPool.GetPooledDictionary<string, IPooledDictionary<DrawingTargetContext, IPooledList<OngekiTimelineObjectBase>>>();
        var usedDrawingContexts = ObjectPool.GetPooledSet<int>();
        var unusedSoflanGroups = ObjectPool.GetPooledList<int>();

        try
        {
            //always draw default soflan group
            usedDrawingContexts.Add(0);

            //Prepare objects we will draw them.
            //get&register all visible objects for every drawingContext(soflanGroup)
            //Prepare objects we will draw them.
            //get&register all visible objects for every drawingContext(soflanGroup)
            //帧首算一次「所有组的可见区间」并缓存：它同时服务于下面的全局枚举，
            //以及本帧随后被逐个可见子物体调用的 CheckRangeVisible()。
            foreach (var ctx in drawingContexts.Values)
            {
                foreach (var range in ctx.VisibleTGridRanges)
                    mergedVisibleTGridRanges.Add(range);
            }

            var allVisibleTGridRanges = mergedVisibleTGridRanges.Merge();
            using var visibleObjects = EnumerateAllDisplayableObjects(fumen, allVisibleTGridRanges);
            foreach (var displayable in visibleObjects)
            {
                if (displayable is not OngekiTimelineObjectBase obj)
                    continue;

                if (!map.TryGetValue(obj.IDShortName, out var soflanGroupObjectMap))
                {
                    var soflanGroupObjectMapPool = ObjectPool.GetPooledDictionary<DrawingTargetContext, IPooledList<OngekiTimelineObjectBase>>();
                    drawingCollectionDisposables.Add(soflanGroupObjectMapPool);
                    soflanGroupObjectMap = map[obj.IDShortName] = soflanGroupObjectMapPool;
                }

                _cacheSoflanGroupRecorder.GetCache(obj.Id, out var soflanGroup);

                if (!CheckSoflanGroupVisible(soflanGroup))
                    continue;

                if (drawingContexts.TryGetValue(soflanGroup, out var drawingContext))
                {
                    if (!soflanGroupObjectMap.TryGetValue(drawingContext, out var list))
                    {
                        var listPool = ObjectPool.GetPooledList<OngekiTimelineObjectBase>();
                        drawingCollectionDisposables.Add(listPool);
                        list = soflanGroupObjectMap[drawingContext] = listPool;
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
                        var resultMapPool = ObjectPool.GetPooledDictionary<DrawingTargetContext, IPooledList<OngekiObjectBase>>();
                        drawingCollectionDisposables.Add(resultMapPool);
                        var resultMap = drawMap[drawingTarget] = resultMapPool;
                        foreach (var pair in soflanGroupObjectMap)
                        {
                            var objectListPool = ObjectPool.GetPooledList<OngekiObjectBase>();
                            drawingCollectionDisposables.Add(objectListPool);
                            var objectList = resultMap[pair.Key] = objectListPool;
                            objectList.AddRange(pair.Value);
                        }
                    }
                    else
                    {
                        foreach (var pair in soflanGroupObjectMap)
                        {
                            if (!enums.TryGetValue(pair.Key, out var rr))
                            {
                                var objectListPool = ObjectPool.GetPooledList<OngekiObjectBase>();
                                drawingCollectionDisposables.Add(objectListPool);
                                rr = enums[pair.Key] = objectListPool;
                            }

                            rr.AddRange(pair.Value);
                        }
                    }
                }
            }

            //remove unused drawingContexts
            unusedSoflanGroups.AddRange(drawingContexts.Keys.Except(usedDrawingContexts));
            foreach (var soflanGroupId in unusedSoflanGroups)
                drawingContexts.Remove(soflanGroupId);

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
                    var resultMapPool = ObjectPool.GetPooledDictionary<DrawingTargetContext, IPooledList<OngekiObjectBase>>();
                    drawingCollectionDisposables.Add(resultMapPool);
                    var resultMap = drawMap[drawingTarget] = resultMapPool;
                    var objectListPool = ObjectPool.GetPooledList<OngekiObjectBase>();
                    drawingCollectionDisposables.Add(objectListPool);
                    var objectList = resultMap[defaultDrawingTargetContext] = objectListPool;
                    objectList.AddRange(blts);
                }
                foreach (var drawingTarget in GetDrawingTarget(Bell.CommandName))
                {
                    //todo 优化一下
                    var resultMapPool = ObjectPool.GetPooledDictionary<DrawingTargetContext, IPooledList<OngekiObjectBase>>();
                    drawingCollectionDisposables.Add(resultMapPool);
                    var resultMap = drawMap[drawingTarget] = resultMapPool;
                    var objectListPool = ObjectPool.GetPooledList<OngekiObjectBase>();
                    drawingCollectionDisposables.Add(objectListPool);
                    var objectList = resultMap[defaultDrawingTargetContext] = objectListPool;
                    objectList.AddRange(bels);
                }
            }

            #region Rendering

            CurrentDrawingTargetContext = defaultDrawingTargetContext;
            builder?.SetCurrentRect(CurrentDrawingTargetContext.ViewRelativeRect);
            builder?.SetCurrentViewMatrix(CurrentDrawingTargetContext.ViewMatrix);
            builder?.SetCurrentProjectionMatrix(CurrentDrawingTargetContext.ProjectionMatrix);

            foreach (var (minTGrid, maxTGrid) in CurrentDrawingTargetContext.VisibleTGridRanges)
                playableAreaHelper.DrawPlayField(this, builder, minTGrid, maxTGrid);

            playableAreaHelper.Draw(this, builder);
            timeSignatureHelper.DrawLines(this, builder);

            xGridHelper.DrawLines(this, builder, CachedMagneticXGridLines);

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
                builder?.SetCurrentRect(CurrentDrawingTargetContext.ViewRelativeRect);
                builder?.SetCurrentViewMatrix(CurrentDrawingTargetContext.ViewMatrix);
                builder?.SetCurrentProjectionMatrix(CurrentDrawingTargetContext.ProjectionMatrix);

                {
                    if (drawMap.TryGetValue(drawingTarget, out var drawingObjs))
                    {
                        foreach (var soflanGroupDrawing in drawingObjs)
                        {
                            CurrentDrawingTargetContext = soflanGroupDrawing.Key;
                            builder?.SetCurrentRect(CurrentDrawingTargetContext.ViewRelativeRect);
                            builder?.SetCurrentViewMatrix(CurrentDrawingTargetContext.ViewMatrix);
                            builder?.SetCurrentProjectionMatrix(CurrentDrawingTargetContext.ProjectionMatrix);

                            drawingTarget.Begin(this, builder);
                            //all object collection has been sorted within GetDisplayableObjects()
                            foreach (var obj in soflanGroupDrawing.Value/*.OrderBy(x => x.TGrid)*/)
                                drawingTarget.Post(obj);
                            drawingTarget.End();
                        }
                    }
                }
            }

            CurrentDrawingTargetContext = defaultDrawingTargetContext;
            builder?.SetCurrentRect(CurrentDrawingTargetContext.ViewRelativeRect);
            builder?.SetCurrentViewMatrix(CurrentDrawingTargetContext.ViewMatrix);
            builder?.SetCurrentProjectionMatrix(CurrentDrawingTargetContext.ProjectionMatrix);

            timeSignatureHelper.DrawTimeSigntureText(this, builder);
            xGridHelper.DrawXGridText(this, builder, CachedMagneticXGridLines);
            judgeLineHelper.Draw(this, builder);
            hitObjectEffectHelper.Draw(this, builder);
            playerLocationHelper.Draw(this, builder);
            selectingRangeHelper.Draw(this, builder);

            PostDrawCommandList(builder);

            #endregion
        }
        finally
        {
            for (var i = drawingCollectionDisposables.Count - 1; i >= 0; i--)
                drawingCollectionDisposables[i].Dispose();

            unusedSoflanGroups.Dispose();
            usedDrawingContexts.Dispose();
            map.Dispose();
            drawingCollectionDisposables.Dispose();
            // drawMap 的唯一一处「帧末清理」：本帧填充的池化内层对象已在上面的循环里逐个 Dispose，
            // 这里只需清掉外层字典的键，使下一帧从空态重新填充（外层字典本身跨帧复用，不重建）。
            // 成功路径与异常路径都经过本块；try 之前的两个 goto End 早退点不经此处，但那时 drawMap 本就为空。
            drawMap.Clear();
        }

        // Freeze this frame's registered hit rects into an immutable snapshot for the UI thread.
        // Placed on the success path only: a frame skipped by the FPS gate (goto End) or aborted by an
        // exception keeps the previous complete snapshot instead of publishing a partial/empty one.
        CommitHitObjects();

    End:
        builder?.Dispose();
        // 这里**不再** drawMap.Clear()：本帧对 drawMap 的写入全部发生在 try 内（:510/:514/:583/:594），
        // 而到达本标签只有两条路径 —— 正常出帧（先经过 finally 的 Clear）或 try 前早退
        // （:314 限帧丢弃 / :346 fumen 为 null，此时 drawMap 尚未被本帧触碰、本就为空）。
        // try 内抛异常时 finally 执行后异常继续上抛，不会落到这里。
        // 故 finally 那一处已覆盖全部可达路径，此次 Clear 恒为冗余（详见 RND-C3 审计条目）。
        //set null
        CurrentDrawingTargetContext = default;
    }

    private void PostDrawCommandList(IDrawCommandListBuilder builder)
    {
        if (builder is null)
            return;

        var drawCommandList = builder.GetDrawCommandList();
        var renderContext = RenderContext;
        if (renderContext is null)
        {
            drawCommandList.Dispose();
            return;
        }

        try
        {
            renderContext.PostDrawCommandList(drawCommandList, autoDispose: true);
        }
        catch
        {
            drawCommandList.Dispose();
            throw;
        }
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

    /// <summary>
    /// 该 TGrid 是否落在任一 soflan 组的可见区间内。
    /// 会被逐个可见子物体调用，故只遍历 Dictionary.Values（struct 枚举器，无快照分配、
    /// 无接口派发），不再为只读枚举付 ConcurrentDictionary 的钱。
    /// </summary>
    public bool CheckVisible(TGrid tGrid)
    {
        foreach (var ctx in drawingContexts.Values)
        {
            if (CheckVisible(ctx, tGrid))
                return true;
        }

        return false;
    }

    /// <summary>
    /// [minTGrid, maxTGrid] 是否与任一 soflan 组的可见区间相交。
    /// 语义上等价于「遍历所有组的全部可见区间」，因此直接扫描帧首预合并的
    /// <see cref="mergedVisibleTGridRanges"/>，不必每次都去触碰 drawingContexts。
    /// </summary>
    public bool CheckRangeVisible(TGrid minTGrid, TGrid maxTGrid)
    {
        for (var i = 0; i < mergedVisibleTGridRanges.Count; i++)
        {
            var visibleRange = mergedVisibleTGridRanges[i];
            if (!(minTGrid > visibleRange.maxTGrid || maxTGrid < visibleRange.minTGrid))
                return true;
        }

        return false;
    }

    public IRenderContext RenderContext { get; private set; }


    public override void OnViewAfterLoaded(IView view)
    {
        base.OnViewAfterLoaded(view);
        AttachRuntimeSubscriptions();
        View = view as FumenVisualEditorView;
        FlushPendingToast();
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

    private IPooledList<IDisplayableObject> EnumerateAllDisplayableObjects(OngekiFumen fumen,
        IEnumerable<(TGrid min, TGrid max)> visibleRanges)
    {
        var result = ObjectPool.GetPooledList<IDisplayableObject>();
        try
        {
            var containBeams = fumen.Beams.Any();
            var judgeTGrid = GetCurrentTGrid();
            var isPreviewMode = IsPreviewMode;

            IEnumerable<BPMChange> filterFirstBpm = [fumen.BpmList.FirstOrDefault()];
            IEnumerable<MeterChange> filterMeterChange = [fumen.MeterChanges.FirstMeter];

            foreach (var (min, max) in visibleRanges)
            {
                AppendDisplayables(result, fumen.MeterChanges.BinaryFindRange(min, max).Except(filterMeterChange)); //not show first meter
                AppendDisplayables(result, fumen.BpmList.BinaryFindRange(min, max).Except(filterFirstBpm)); //not show first bpm
                AppendDisplayables(result, fumen.ClickSEs.BinaryFindRange(min, max));
                AppendDisplayables(result, fumen.LaneBlocks.GetVisibleStartObjects(min, max));
                AppendDisplayables(result, fumen.Comments.BinaryFindRange(min, max));

                foreach (var soflan in fumen.SoflansMap.Values)
                    AppendDisplayables(result, soflan.GetVisibleStartObjects(min, max));
                foreach (var area in fumen.IndividualSoflanAreaMap.Values)
                    AppendDisplayables(result, area.GetVisibleStartObjects(min, max));

                AppendDisplayables(result, fumen.EnemySets.BinaryFindRange(min, max));
                // SVG prefabs are temporarily excluded from the editor drawing/object-selection pipeline.
                // AppendDisplayables(result, fumen.SvgPrefabs.BinaryFindRange(min, max));
                AppendDisplayables(result, fumen.Lanes.GetVisibleStartObjects(min, max));

                foreach (var hold in fumen.Holds.GetVisibleStartObjects(min, max))
                {
                    if (isPreviewMode && !(hold.EndTGrid > judgeTGrid))
                        continue;
                    AppendOne(result, hold);
                }

                foreach (var flick in fumen.Flicks.BinaryFindRange(min, max))
                {
                    if (isPreviewMode && !(flick.TGrid > judgeTGrid))
                        continue;
                    AppendOne(result, flick);
                }

                foreach (var tap in fumen.Taps.BinaryFindRange(min, max))
                {
                    if (isPreviewMode && !(tap.TGrid > judgeTGrid))
                        continue;
                    AppendOne(result, tap);
                }

                if (containBeams)
                {
                    var leadInTGrid = TGridCalculator.ConvertAudioTimeToTGrid(
                        TGridCalculator.ConvertTGridToAudioTime(min, this) -
                        TGridCalculator.ConvertFrameToAudioTime(BeamStart.LEAD_IN_DURATION_FRAME), this);
                    var leadOutTGrid = TGridCalculator.ConvertAudioTimeToTGrid(
                        TGridCalculator.ConvertTGridToAudioTime(max, this) +
                        TimeSpan.FromMilliseconds(BeamStart.LEAD_OUT_DURATION), this);
                    AppendDisplayables(result, fumen.Beams.GetVisibleStartObjects(leadInTGrid, leadOutTGrid));
                }

                /*
                 * 这里考虑到有spd<1的子弹/Bell会提前出现的情况，因此得分状态分别去选择
                 */
                if (!isPreviewMode)
                {
                    foreach (var bell in fumen.Bells.BinaryFindRange(min, max))
                        AppendOne(result, bell);
                    foreach (var bullet in fumen.Bullets.BinaryFindRange(min, max))
                        AppendOne(result, bullet);
                }
            }

            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private static void AppendDisplayables<T>(IPooledList<IDisplayableObject> list, IEnumerable<T> objects)
        where T : IDisplayableObject
    {
        foreach (var obj in objects)
            AppendOne(list, obj);
    }

    private static void AppendOne(IPooledList<IDisplayableObject> list, IDisplayableObject obj)
    {
        foreach (var displayable in obj.GetDisplayableObjects())
            list.Add(displayable);
    }

    private System.Numerics.Vector4 GetCleanColor()
    {
        var cleanColor = System.Numerics.Vector4.Zero;
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

        return cleanColor;
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

    /// <summary>
    /// 单组可见性查询。注意语义与 <see cref="CheckRangeVisible(TGrid, TGrid)"/> 不同：
    /// 这里只看传入 context 自己的区间，调用方若要「任一组可见」请用无 context 的重载。
    /// </summary>
    public bool CheckVisible(DrawingTargetContext context, TGrid tGrid)
    {
        foreach (var (minTGrid, maxTGrid) in context.VisibleTGridRanges)
            if (minTGrid <= tGrid && tGrid <= maxTGrid)
                return true;
        return false;
    }

    /// <summary>
    /// 保留原重载签名与「忽略 context、查任意组是否可见」的既有语义（调用方
    /// <see cref="Graphics.Drawing.TargetImpl.OngekiObjects.LaneBlockerDrawingTarget"/> 依赖该语义），
    /// 但改为扫描帧首预合并的缓存，不再每次 SelectMany 摊平整个字典。
    /// </summary>
    public bool CheckRangeVisible(DrawingTargetContext context, TGrid minTGrid, TGrid maxTGrid)
        => CheckRangeVisible(minTGrid, maxTGrid);

    private ActionExecutionContext CreateExecutionContext(object source, object eventArgs)
        => new() { Source = source, EventArgs = eventArgs, View = View };

    public Task InitializeRenderControlAsync(ContentControl contentControl)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return InitializeRenderControlAsync(
            contentControl,
            IoC.Get<IRenderManager>().GetCurrentRenderManagerImpl(),
            IoC.GetAll<IFumenEditorDrawingTarget>());
    }

    internal async Task InitializeRenderControlAsync(
        ContentControl contentControl,
        IRenderManagerImpl newRenderImpl,
        IEnumerable<IFumenEditorDrawingTarget> availableDrawingTargets,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(contentControl);
        ArgumentNullException.ThrowIfNull(newRenderImpl);
        ArgumentNullException.ThrowIfNull(availableDrawingTargets);

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
            PrepareRenderLoop(newRenderControl, newRenderImpl, availableDrawingTargets);
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
        renderContext.Name = "FumenVisualEditorViewModel.Render";
        renderContext.OnRender -= OnRenderFrame;
        renderContext.OnRender += OnRenderFrame;
        renderContext.StartRendering();
    }

    private void StopRenderContext()
    {
        var renderContext = RenderContext;
        RenderContext = null;
        if (renderContext is null)
            return;

        renderContext.OnRender -= OnRenderFrame;
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
        mergedVisibleTGridRanges.Clear();
        cachedMagneticXGridLines.Clear();
        CurrentDrawingTargetContext = null;
        sw?.Stop();
        sw = null;
        renderInitializationTaskSource.TrySetResult();
    }

    public Task WaitForRenderInitializationIsDone()
    {
        return renderInitializationTaskSource.Task;
    }
}



