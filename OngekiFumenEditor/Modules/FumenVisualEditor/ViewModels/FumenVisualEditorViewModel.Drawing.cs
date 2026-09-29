using Caliburn.Micro;
using ControlzEx.Standard;
using Gemini.Framework;
using NAudio.Gui;
using NWaves.Utils;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using static OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors.DrawXGridHelper;
using Color = System.Drawing.Color;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

public partial class FumenVisualEditorViewModel : PersistedDocument, ISchedulable, IFumenEditorDrawingContext
{
    private Dictionary<string, IFumenEditorDrawingTarget[]> drawTargetMap = new();

    private readonly List<CacheDrawXLineResult> cachedMagneticXGridLines = new();

    private Func<double, FumenVisualEditorViewModel, SoflanList, double>
        convertToY = (tUnit, editor, _) => editor.ConvertTGridUnitToY_DesignMode(tUnit);

    private string debugInfo = "";
    private readonly Dictionary<IFumenEditorDrawingTarget, IPooledDictionary<DrawingTargetContext, IPooledList<OngekiObjectBase>>> drawMap = new();
    private IFumenEditorDrawingTarget[] drawTargetOrder;
    private bool enablePlayFieldDrawing;

    private bool showDebugInfo;
    private DrawJudgeLineHelper judgeLineHelper;
    private DrawPlayableAreaHelper_new playableAreaHelper;
    internal GlobalCacheSoflanGroupRecorder _cacheSoflanGroupRecorder = new();
    private DrawHitObjectEffectHelper hitObjectEffectHelper;
    private DrawPlayerLocationHelper playerLocationHelper;
    private Vector4 playFieldBackgroundColor;

    private DrawSelectingRangeHelper selectingRangeHelper;

    private readonly StringBuilder stringBuilder = new(2048);

    private DrawTimeSignatureHelper timeSignatureHelper;

    private float viewHeight;
    private float viewWidth;
    private float renderScaleX = 1;
    private float renderScaleY = 1;

    private DrawXGridHelper xGridHelper;
    private int cacheMagaticXGridLinesHash;

    private IEnumerable<IFumenEditorDrawingTarget> drawingTargets = [];
    public IEnumerable<IFumenEditorDrawingTarget> CurrentDrawingTargets => drawingTargets;

    private TaskCompletionSource renderInitializationTaskSource = new();
    private TaskCompletionSource renderFirstFrameTaskSource = new();

    private VisibleRect rectInDesignMode;
    public VisibleRect RectInDesignMode
    {
        get => rectInDesignMode;
        set
        {
            Set(ref rectInDesignMode, value);
            NotifyOfPropertyChange(() => RectInDesignMode);
        }
    }

    public IEnumerable<CacheDrawXLineResult> CachedMagneticXGridLines => cachedMagneticXGridLines;

    public PlayerLocationRecorder PlayerLocationRecorder { get; } = new();

    public bool ShowDebugInfo
    {
        get => showDebugInfo;
        set
        {
            Set(ref showDebugInfo, value);
        }
    }

    public string DebugInfo
    {
        get => debugInfo;
        set
        {
            debugInfo = value;
            NotifyOfPropertyChange(() => DebugInfo);
        }
    }

    public float ViewWidth
    {
        get => viewWidth;
        set
        {
            Set(ref viewWidth, value);
        }
    }

    public float ViewHeight
    {
        get => viewHeight;
        set
        {
            Set(ref viewHeight, value);
        }
    }

    public float RenderScaleX
    {
        get => renderScaleX;
        set => Set(ref renderScaleX, value);
    }

    public float RenderScaleY
    {
        get => renderScaleY;
        set => Set(ref renderScaleY, value);
    }

    public DrawingTargetContext CurrentDrawingTargetContext { get; set; }

    public TimeSpan CurrentPlayTime { get; private set; } = TimeSpan.FromSeconds(0);

    public FumenVisualEditorViewModel Editor => this;

    public IPerfomenceMonitor PerfomenceMonitor => DummyPerformenceMonitor.Instance;

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
        var dpi = VisualTreeHelper.GetDpi(renderControl);
        RenderScaleX = (float)dpi.DpiScaleX;
        RenderScaleY = (float)dpi.DpiScaleY;

        ViewWidth = (float)renderControl.ActualWidth;
        ViewHeight = (float)renderControl.ActualHeight;

        playFieldBackgroundColor = Color.FromArgb(EditorGlobalSetting.Default.PlayFieldBackgroundColor).ToVector4();
        enablePlayFieldDrawing = EditorGlobalSetting.Default.EnablePlayFieldDrawing;
        hideWallLaneWhenEnablePlayField = EditorGlobalSetting.Default.HideWallLaneWhenEnablePlayField;

        //get and initialize drawing targets.
        drawingTargets = IoC.GetAll<IFumenEditorDrawingTarget>();
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

        playableAreaHelper = new DrawPlayableAreaHelper_new();
        playableAreaHelper.Initalize(renderImpl);

        playerLocationHelper = new DrawPlayerLocationHelper();
        playerLocationHelper.Initalize(renderImpl);

        hitObjectEffectHelper = new DrawHitObjectEffectHelper();
        hitObjectEffectHelper.Initalize(renderImpl);

        UpdateActualRenderInterval();

        renderInitializationTaskSource.SetResult();
    }

    private void OnEditorLoop(IRenderContext context, TimeSpan ts)
    {
        // 第一帧已进入渲染循环，编辑器加载流程据此判定「已就绪」。
        renderFirstFrameTaskSource.TrySetResult();

        //todo update() not should be in render loop
        using (EnterRenderDataWriteLock())
            OnEditorUpdate(ts);

        using (EnterRenderDataReadLock())
            OnEditorRender(context, ts);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Render(IRenderContext context, TimeSpan ts)
        => OnEditorLoop(context, ts);

    Dictionary<int, DrawingTargetContext> drawingContexts = new();

    /// <summary>
    /// 本帧「所有组可见 TGrid 区间」的合并输入（帧首计算一次）。
    /// <see cref="CheckRangeVisible(TGrid, TGrid)"/> 会被逐个可见子物体调用，
    /// 不能每次都去遍历 drawingContexts 现算，故这里缓存一份。
    /// </summary>
    private readonly List<(TGrid minTGrid, TGrid maxTGrid)> mergedVisibleTGridRanges = new();

    private IRenderManagerImpl renderImpl;

    private void UpdateActualRenderInterval()
    {
        if (RenderContext is null)
            return;

        var limitFPS = EditorGlobalSetting.Default.LimitFPS;
        RenderContext.LimitFPS = limitFPS <= 0 ? -1 : limitFPS;
    }

    private void OnEditorRender(IRenderContext context, TimeSpan ts)
    {
        IDrawCommandListBuilder builder = default;

        hits.Clear();

        drawingContexts.Clear();
        mergedVisibleTGridRanges.Clear();
        CurrentDrawingTargetContext = default;

        if (RenderContext is null || renderImpl is null)
            goto End;

        builder = renderImpl.CreateDrawCommandListBuilder();
        builder.SetCleanColor(GetCleanColor());
        builder.SetViewport(ViewWidth, ViewHeight, RenderScaleX, RenderScaleY);

        var projectionMatrix =
            Matrix4x4.CreateOrthographic(ViewWidth, ViewHeight, -1, 1);
        builder.SetCurrentViewMatrix(Matrix4x4.Identity);
        builder.SetCurrentProjectionMatrix(projectionMatrix);

        var fumen = Fumen;
        if (fumen is null)
        {
            PostDrawCommandList(builder);
            goto End;
        }

        context.PerfomenceMonitor.OnBeforeRender();

        //计算可以显示的TGrid范围以及像素范围

        // 帧首唯一一次读取播放时间：整帧（原点、裁判线、特效、拍线……）都基于同一快照，
        // 命令生成与随后呈现共享这一帧的时间基准；帧内不再重复读取，渲染线程与 UI 线程的
        // 时间推进因此不会被撕裂进同一帧。
        var frameTime = CurrentPlayTime;
        var frameTGrid = ConvertAudioTimeToTGrid(frameTime) ?? TGrid.Zero;
        // 预览模式下视口由滚动位置决定（尾段余量允许它领先于被钳制的播放时间），
        // 设计模式下两者相同 —— 与旧 GetViewportTGrid() / GetViewportAudioTime() 的分支一致。
        var previewScrollMs = previewScrollPositionMs;
        var frameViewportTime = IsPreviewMode ? TimeSpan.FromMilliseconds(previewScrollMs) : frameTime;
        var frameViewportTGrid = IsPreviewMode ? (ConvertAudioTimeToTGrid(frameViewportTime) ?? TGrid.Zero) : frameTGrid;

        var tGrid = frameViewportTGrid;
        var offsetMs = EditorGlobalSetting.Default.EditorOffsetMs;
        if (offsetMs != 0)
        {
            var actualMs = frameViewportTime + TimeSpan.FromMilliseconds(offsetMs);
            if (actualMs < TimeSpan.Zero)
                actualMs = TimeSpan.Zero;
            tGrid = TGridCalculator.ConvertAudioTimeToTGrid(actualMs, Fumen.BpmList) ?? TGrid.Zero;
        }

        #region prepare drawing contexts' for every soflan groups

        IEnumerable<KeyValuePair<int, SoflanList>> soflanMap = Fumen.SoflansMap;
        //if (IsDesignMode)
        //    soflanMap = [new KeyValuePair<int, SoflanList>(0, Fumen.SoflansMap.DefaultSoflanList)];

        foreach (KeyValuePair<int, SoflanList> pair in soflanMap)
        {
            var curY = ConvertToY(tGrid.TotalUnit, pair.Value);
            var minY = curY - Setting.JudgeLineOffsetY;
            var maxY = minY + ViewHeight;

            var visibleTGridRanges = new SortableCollection<(TGrid minTGrid, TGrid maxTGrid), TGrid>(x => x.minTGrid);

            if (IsPreviewMode)
            {
                //Preview Mode
                using var ranges =
                    pair.Value.GetVisibleRanges_PreviewMode(curY, ViewHeight, Setting.JudgeLineOffsetY, Fumen.BpmList,
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
                var minTGrid = ConvertYToTGrid_DesignMode(minY) ?? TGrid.Zero;
                var maxTGrid = ConvertYToTGrid_DesignMode(maxY) ?? TGrid.Zero;
                visibleTGridRanges.Add((minTGrid, maxTGrid));
            }

            var worldRect = new VisibleRect(new Vector2(ViewWidth, (float)minY), new Vector2(0, (float)maxY));
            var viewRelativeRect = new VisibleRect(new Vector2(ViewWidth, 0), new Vector2(0, ViewHeight));

            var viewMatrix = Matrix4x4.CreateTranslation(new Vector3(-ViewWidth / 2, -ViewHeight / 2, 0));

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
                ViewportTGrid = frameViewportTGrid,
                ViewMatrix = viewMatrix,
                ProjectionMatrix = projectionMatrix,
                ViewWidth = ViewWidth,
                ViewHeight = ViewHeight,
                RenderScaleX = RenderScaleX,
                RenderScaleY = RenderScaleY
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
            //帧首算一次「所有组的可见区间」并缓存：它同时服务于下面的全局枚举，
            //以及本帧随后被逐个可见子物体调用的 CheckRangeVisible()。
            void MaterializeMergedVisibleTGridRanges()
            {
                mergedVisibleTGridRanges.Clear();
                foreach (var ctx in drawingContexts.Values)
                {
                    foreach (var range in ctx.VisibleTGridRanges)
                        mergedVisibleTGridRanges.Add(range);
                }
            }

            MaterializeMergedVisibleTGridRanges();

            var allVisibleTGridRanges = mergedVisibleTGridRanges.Merge();
            using (var visibleObjects = EnumerateAllDisplayableObjects(fumen, allVisibleTGridRanges, frameTGrid))
            {
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
                        Log.LogWarn($"Soflan group drawing context not found: object={obj.GetType().Name}, objectId={obj.Id}, soflanGroup={soflanGroup}");
                    }
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
                        var rPool = ObjectPool.GetPooledDictionary<DrawingTargetContext, IPooledList<OngekiObjectBase>>();
                        drawingCollectionDisposables.Add(rPool);
                        var r = drawMap[drawingTarget] = rPool;
                        foreach (var pair in soflanGroupObjectMap)
                        {
                            var rrPool = ObjectPool.GetPooledList<OngekiObjectBase>();
                            drawingCollectionDisposables.Add(rrPool);
                            var rr = r[pair.Key] = rrPool;
                            rr.AddRange(pair.Value);
                        }
                    }
                    else
                    {
                        foreach (var pair in soflanGroupObjectMap)
                        {
                            if (!enums.TryGetValue(pair.Key, out var rr))
                            {
                                var rrPool = ObjectPool.GetPooledList<OngekiObjectBase>();
                                drawingCollectionDisposables.Add(rrPool);
                                rr = enums[pair.Key] = rrPool;
                            }

                            rr.AddRange(pair.Value);
                        }
                    }
                }
            }

            //remove unused drawingContexts
            unusedSoflanGroups.AddRange(drawingContexts.Keys.Except(usedDrawingContexts));
            for (var i = 0; i < unusedSoflanGroups.Count; i++)
            {
                var soflanGroupId = unusedSoflanGroups[i];
                drawingContexts.Remove(soflanGroupId);
            }

            // 未被任何可见对象使用的 soflan 组已经从 drawingContexts 摘掉：重建帧内区间缓存，
            // 让 CheckRangeVisible 与旧实现（调用时现读 drawingContexts）看到同一集合，
            // 否则已移除组的区间仍会让 LaneBlocker / VisibleLineVerticesQuery 多做几何提交。
            // 本帧的对象枚举已经在上面的 using 块内完成，重建不会影响它。
            MaterializeMergedVisibleTGridRanges();

            RecalculateMagaticXGridLines();

            if (IsPreviewMode)
            {
                //特殊处理：子弹和Bell（本块仅预览模式：只取当前时间之后的）
                var blts = Fumen.Bullets.BinaryFindRange(frameTGrid, TGrid.MaxValue);
                var bels = Fumen.Bells.BinaryFindRange(frameTGrid, TGrid.MaxValue);
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
                    var rPool = ObjectPool.GetPooledDictionary<DrawingTargetContext, IPooledList<OngekiObjectBase>>();
                    drawingCollectionDisposables.Add(rPool);
                    var r = drawMap[drawingTarget] = rPool;
                    var rrPool = ObjectPool.GetPooledList<OngekiObjectBase>();
                    drawingCollectionDisposables.Add(rrPool);
                    var rr = r[defaultDrawingTargetContext] = rrPool;
                    rr.AddRange(blts);
                }
                foreach (var drawingTarget in GetDrawingTarget(Bell.CommandName))
                {
                    //todo 优化一下
                    var rPool = ObjectPool.GetPooledDictionary<DrawingTargetContext, IPooledList<OngekiObjectBase>>();
                    drawingCollectionDisposables.Add(rPool);
                    var r = drawMap[drawingTarget] = rPool;
                    var rrPool = ObjectPool.GetPooledList<OngekiObjectBase>();
                    drawingCollectionDisposables.Add(rrPool);
                    var rr = r[defaultDrawingTargetContext] = rrPool;
                    rr.AddRange(bels);
                }
            }

            #region Rendering

            CurrentDrawingTargetContext = defaultDrawingTargetContext;
            builder.SetCurrentRect(CurrentDrawingTargetContext.ViewRelativeRect);
            builder.SetCurrentViewMatrix(CurrentDrawingTargetContext.ViewMatrix);
            builder.SetCurrentProjectionMatrix(CurrentDrawingTargetContext.ProjectionMatrix);

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
                builder.SetCurrentRect(CurrentDrawingTargetContext.ViewRelativeRect);
                builder.SetCurrentViewMatrix(CurrentDrawingTargetContext.ViewMatrix);
                builder.SetCurrentProjectionMatrix(CurrentDrawingTargetContext.ProjectionMatrix);

                if (drawMap.TryGetValue(drawingTarget, out var drawingObjs))
                {
                    foreach (var soflanGroupDrawing in drawingObjs)
                    {
                        CurrentDrawingTargetContext = soflanGroupDrawing.Key;
                        builder.SetCurrentRect(CurrentDrawingTargetContext.ViewRelativeRect);
                        builder.SetCurrentViewMatrix(CurrentDrawingTargetContext.ViewMatrix);
                        builder.SetCurrentProjectionMatrix(CurrentDrawingTargetContext.ProjectionMatrix);

                        drawingTarget.Begin(this, builder);
                        //all object collection has been sorted within GetDisplayableObjects()
                        var drawingObjects = soflanGroupDrawing.Value;
                        for (var i = 0; i < drawingObjects.Count; i++)
                        {
                            var obj = drawingObjects[i];
                            drawingTarget.Post(obj);
                        }
                        drawingTarget.End();
                    }
                }
            }

            CurrentDrawingTargetContext = defaultDrawingTargetContext;
            builder.SetCurrentRect(CurrentDrawingTargetContext.ViewRelativeRect);
            builder.SetCurrentViewMatrix(CurrentDrawingTargetContext.ViewMatrix);
            builder.SetCurrentProjectionMatrix(CurrentDrawingTargetContext.ProjectionMatrix);

            timeSignatureHelper.DrawTimeSigntureText(this, builder);
            xGridHelper.DrawXGridText(this, builder, CachedMagneticXGridLines);
            judgeLineHelper.Draw(this, builder);
            hitObjectEffectHelper.Draw(this, builder);
            playerLocationHelper.Draw(this, builder);
            selectingRangeHelper.Draw(this, builder);

            PostDrawCommandList(builder);

        //clean up
        }
        finally
        {
            for (var i = 0; i < drawingCollectionDisposables.Count; i++)
            {
                var disposable = drawingCollectionDisposables[i];
                disposable.Dispose();
            }
            unusedSoflanGroups.Dispose();
            usedDrawingContexts.Dispose();
            map.Dispose();
            drawingCollectionDisposables.Dispose();
            // drawMap 的唯一一处「帧末清理」：本帧填充的池化内层对象已在上面的循环里逐个 Dispose，
            // 这里只需清掉外层字典的键，使下一帧从空态重新填充（外层字典本身跨帧复用，不重建）。
            // 成功路径与异常路径都经过本块；try 之前的两个 goto End 早退点不经此处，但那时 drawMap 本就为空。
            drawMap.Clear();
        }
        context.PerfomenceMonitor.OnAfterRender();
        #endregion
    End:
        builder?.Dispose();
        // 这里**不再** drawMap.Clear()：本帧对 drawMap 的写入全部发生在 try 内，而到达本标签只有两条路径 ——
        // 正常出帧（先经过 finally 的 Clear）或 try 前早退（此时 drawMap 尚未被本帧触碰、本就为空）。
        //set null
        CurrentDrawingTargetContext = default;
    }

    public bool CheckDrawingVisible(DrawingVisible visible)
    {
        return visible.HasFlag(EditorObjectVisibility == Visibility.Visible
            ? DrawingVisible.Design
            : DrawingVisible.Preview);
    }

    public double ConvertToY(double tGridUnit, SoflanList soflanList)
        => convertToY(tGridUnit, this, soflanList);

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

    public string SchedulerName => "Fumen Previewer Debug Info";

    public TimeSpan ScheduleCallLoopInterval => TimeSpan.FromSeconds(1);

    public IRenderContext RenderContext { get; private set; }

    public void OnSchedulerTerm()
    {
    }

    public async Task OnScheduleCall(CancellationToken cancellationToken)
    {
        if (ShowDebugInfo)
        {
            stringBuilder.Clear();

            stringBuilder.AppendLine($"Viewport: {ViewWidth}x{ViewHeight}");
            stringBuilder.AppendLine($"VisibleRanges ({drawingContexts.Count} sfl groups):");

            foreach (var item in drawingContexts.OrderBy(x => x.Key))
            {
                var ranges = item.Value?.VisibleTGridRanges;
                if (ranges != null)
                {
                    foreach (var tGridRange in ranges)
                        stringBuilder.AppendLine($"*[{item.Key}]  {tGridRange.minTGrid}  -  {tGridRange.maxTGrid} -> {item.Value.WorldRect.MinY:F2} -  {item.Value.WorldRect.MaxY:F2}");

                }
            }

            if (IsPreviewMode)
            {
                var view = GetView() as FrameworkElement;
                if (view is not null)
                {
                    await view.Dispatcher.InvokeAsync(() =>
                    {
                        stringBuilder.AppendLine();
                        if (drawingContexts.ElementAtOrDefault(0).Value?.WorldRect.MaxY - Mouse.GetPosition(view).Y is double mouseY)
                        {
                            stringBuilder.AppendLine($"MouseY: {mouseY:F2}");
                            foreach (var tGrid in ConvertYToTGrid_PreviewMode(mouseY))
                                stringBuilder.AppendLine($"* {tGrid}");
                        }
                    });
                }
            }

            DebugInfo = stringBuilder.ToString();
        }
    }

    protected override void OnViewLoaded(object v)
    {
        base.OnViewLoaded(v);
        InitExtraMenuItems();
    }

    private void ResortRenderOrder()
    {
        drawTargetOrder = drawTargetMap.Values.SelectMany(x => x).OrderBy(x => x.CurrentRenderOrder).Distinct().ToArray();
    }

    public IFumenEditorDrawingTarget[] GetDrawingTarget(string name)
    {
        return drawTargetMap.TryGetValue(name, out var drawingTarget) ? drawingTarget : default;
    }

    /// <summary>
    /// 收集 <paramref name="visibleRanges"/> 中所有可见对象的 displayable 展开。
    /// <paramref name="judgeTGrid"/> 由调用方传入本帧的时间快照，避免帧内二次读取播放时间。
    /// </summary>
    private IPooledList<IDisplayableObject> EnumerateAllDisplayableObjects(OngekiFumen fumen,
        IEnumerable<(TGrid min, TGrid max)> visibleRanges, TGrid judgeTGrid)
    {
        var result = ObjectPool.GetPooledList<IDisplayableObject>();
        try
        {
            var containBeams = fumen.Beams.Any();
            var isPreviewMode = IsPreviewMode;
            var editorIsPreviewMode = Editor.IsPreviewMode;

            IEnumerable<BPMChange> filterFirstBpm = [fumen.BpmList.FirstOrDefault()];
            IEnumerable<MeterChange> filterMeterChange = [fumen.MeterChanges.FirstMeter];

            foreach (var (min, max) in visibleRanges)
            {
                AppendDisplayables(result, fumen.MeterChanges.BinaryFindRange(min, max).Except(filterMeterChange)); //not show first meter
                AppendDisplayables(result, fumen.BpmList.BinaryFindRange(min, max).Except(filterFirstBpm)); //not show first bpm
                AppendDisplayables(result, fumen.ClickSEs.BinaryFindRange(min, max));
                AppendDisplayables(result, fumen.LaneBlocks.GetVisibleStartObjects(min, max));
                AppendDisplayables(result, fumen.Comments.BinaryFindRange(min, max));

                foreach (var sof in fumen.SoflansMap.Values)
                    AppendDisplayables(result, sof.GetVisibleStartObjects(min, max));
                foreach (var area in fumen.IndividualSoflanAreaMap.Values)
                    AppendDisplayables(result, area.GetVisibleStartObjects(min, max));

                AppendDisplayables(result, fumen.EnemySets.BinaryFindRange(min, max));
                AppendDisplayables(result, fumen.Lanes.GetVisibleStartObjects(min, max));
                AppendDisplayables(result, fumen.SvgPrefabs);

                // Holds: PreviewMode 下按 EndTGrid > judgeTGrid 过滤
                foreach (var h in fumen.Holds.GetVisibleStartObjects(min, max))
                {
                    if (isPreviewMode && !(h.EndTGrid > judgeTGrid))
                        continue;
                    AppendOne(result, h);
                }

                // Flicks + Taps: PreviewMode 下按 TGrid > judgeTGrid 过滤
                foreach (var f in fumen.Flicks.BinaryFindRange(min, max))
                {
                    if (isPreviewMode && !(f.TGrid > judgeTGrid))
                        continue;
                    AppendOne(result, f);
                }
                foreach (var t in fumen.Taps.BinaryFindRange(min, max))
                {
                    if (isPreviewMode && !(t.TGrid > judgeTGrid))
                        continue;
                    AppendOne(result, t);
                }

                if (containBeams)
                {
                    var leadInTGrid = ConvertAudioTimeToTGrid(
                        ConvertTGridToAudioTime(min) -
                        TGridCalculator.ConvertFrameToAudioTime(BeamStart.LEAD_IN_DURATION_FRAME));
                    var leadOutTGrid = ConvertAudioTimeToTGrid(
                        ConvertTGridToAudioTime(max) +
                        TimeSpan.FromMilliseconds(BeamStart.LEAD_OUT_DURATION));
                    AppendDisplayables(result, fumen.Beams.GetVisibleStartObjects(leadInTGrid, leadOutTGrid));
                }

                /*
                 * 这里考虑到有spd<1的子弹/Bell会提前出现的情况，因此得分状态分别去选择。
                 * 预览模式下不展示。
                 */
                if (!editorIsPreviewMode)
                {
                    foreach (var bel in fumen.Bells.BinaryFindRange(min, max))
                        AppendOne(result, bel);
                    foreach (var blt in fumen.Bullets.BinaryFindRange(min, max))
                        AppendOne(result, blt);
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

    private static void AppendDisplayables<T>(IPooledList<IDisplayableObject> list, IEnumerable<T> objs)
        where T : IDisplayableObject
    {
        foreach (var obj in objs)
        {
            foreach (var d in obj.GetDisplayableObjects())
                list.Add(d);
        }
    }

    private static void AppendOne(IPooledList<IDisplayableObject> list, IDisplayableObject obj)
    {
        foreach (var d in obj.GetDisplayableObjects())
            list.Add(d);
    }

    private Vector4 GetCleanColor()
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
        return cleanColor;
    }

    private void PostDrawCommandList(IDrawCommandListBuilder builder)
    {
        var drawCommandList = builder.GetDrawCommandList();
        var ownsDrawCommandList = true;

        try
        {
            RenderContext.PostDrawCommandList(drawCommandList, autoDispose: true);
            ownsDrawCommandList = false;
        }
        catch
        {
            if (ownsDrawCommandList)
                drawCommandList.Dispose();
            throw;
        }
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

    public bool CheckSoflanGroupVisible(int soflanGroup)
    {
        var soflanGroupWrapItem = Fumen.IndividualSoflanAreaMap.TryGetOrCreateSoflanGroupWrapItem(soflanGroup, out _);
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

    /// <summary>
    /// 保留原重载签名与「忽略 context、查任意组是否可见」的既有语义，
    /// 但改为扫描帧首预合并的缓存，不再每次 SelectMany 摊平整个字典。
    /// </summary>
    public bool CheckRangeVisible(DrawingTargetContext context, TGrid minTGrid, TGrid maxTGrid)
        => CheckRangeVisible(minTGrid, maxTGrid);

    public async void OnRenderControlHostLoaded(ActionExecutionContext executionContext)
    {
        if (executionContext.Source is not ContentControl contentControl)
            throw new InvalidOperationException($"Fumen render control host source must be ContentControl, actual={executionContext.Source?.GetType().FullName}");
        if (renderImpl != null)
            return;

        renderImpl = IoC.Get<IRenderManager>().GetCurrentRenderManagerImpl();
        var renderControl = renderImpl.CreateRenderControl();
        await renderImpl.InitializeRenderControl(renderControl);
        Log.LogDebug($"RenderControl({renderControl.GetHashCode()}) is created");

        renderControl.Loaded += RenderControl_Loaded;
        renderControl.Unloaded += RenderControl_UnLoaded;
        renderControl.SizeChanged += RenderControl_SizeChanged;

        Message.SetAttach(renderControl, "[Event MouseWheel]=[Action OnMouseWheel($executionContext)];             [Event SizeChanged] = [Action OnSizeChanged($executionContext)];             [Event Loaded] = [Action OnLoaded($executionContext)];             [Event PreviewMouseDown] = [Action OnMouseDown($executionContext)];             [Event MouseMove] = [Action OnMouseMove($executionContext)];             [Event PreviewMouseUp] = [Action OnMouseUp($executionContext)];             [Event MouseLeave] = [Action OnMouseLeave($executionContext)];");

        contentControl.Content = renderControl;

        PrepareRenderLoop(renderControl, renderImpl);
    }

    private void RenderControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var renderControl = sender as FrameworkElement;
        Log.LogDebug($"renderControl new size: {e.NewSize} , renderControl.RenderSize = {renderControl.RenderSize}");

        var dpi = VisualTreeHelper.GetDpi(renderControl);
        RenderScaleX = (float)dpi.DpiScaleX;
        RenderScaleY = (float)dpi.DpiScaleY;

        ViewWidth = (float)e.NewSize.Width;
        ViewHeight = (float)e.NewSize.Height;
    }

    private void RenderControl_UnLoaded(object sender, RoutedEventArgs e)
    {
        var renderControl = sender as FrameworkElement;
        Log.LogDebug($"RenderControl({renderControl.GetHashCode()}) is unloaded");

        try
        {
            var context = RenderContext;
            if (context is null)
                return;

            context.OnRender -= Render;
            context.StopRendering();
        }
        catch (Exception ex)
        {
            Log.LogError($"RenderControl_UnLoaded failed: {ex.Message}", ex);
        }
    }

    private async void RenderControl_Loaded(object sender, RoutedEventArgs e)
    {
        var renderControl = sender as FrameworkElement;
        if (renderControl is null)
        {
            Log.LogError($"RenderControl is null");
            return;
        }

        Log.LogDebug($"RenderControl({renderControl.GetHashCode()}) is loaded");

        try
        {
            RenderContext = await renderImpl.GetOrCreateRenderContext(renderControl);
            RenderContext.Name = "FumenVisualEditorViewModel.Render";
            UpdateActualRenderInterval();
            RenderContext.OnRender += Render;
            RenderContext.StartRendering();
        }
        catch (Exception ex)
        {
            Log.LogError($"RenderControl_Loaded failed: {ex.Message}", ex);
        }
    }

    public Task WaitForRenderInitializationIsDone()
    {
        return renderInitializationTaskSource.Task;
    }

    public Task WaitForFirstRenderFrameIsDone()
    {
        return renderFirstFrameTaskSource.Task;
    }

    private void DisposeRenderLoop()
    {
        var context = RenderContext;
        if (context is not null)
        {
            context.OnRender -= Render;
            context.StopRendering();
            context.Name = default;
            context.PerfomenceMonitor = DummyPerformenceMonitor.Instance;

            renderImpl?.RemoveRenderContext(context);
            RenderContext = null;
        }

        DisposeDrawingHelpers();

        // 绘制目标是 DI 单例（其纹理复用/释放的所有权问题另行处理），这里只释放本编辑器持有的
        // 引用与映射；每次渲染循环重建的助手必须随编辑器关闭释放，否则会继续持有原生纹理与全局设置订阅。
        drawingTargets = [];
        drawTargetOrder = [];
        drawTargetMap.Clear();
        drawMap.Clear();
        drawingContexts.Clear();
        mergedVisibleTGridRanges.Clear();
        cachedMagneticXGridLines.Clear();
        CurrentDrawingTargetContext = default;
        renderInitializationTaskSource.TrySetResult();
        renderFirstFrameTaskSource.TrySetResult();
    }

    /// <summary>
    /// 释放每次 <see cref="PrepareRenderLoop"/> 重建的绘制助手（它们持有原生纹理与全局设置订阅）。
    /// </summary>
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
}
