using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Avalonia.Benchmark.Infrastructure;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;
using OngekiFumenEditor.Avalonia.Models.Settings;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.BulletBell;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Utils.ObjectPool;

namespace OngekiFumenEditor.Avalonia.Benchmark.Benchmarks;

/// <summary>
/// 专项问题：<c>DrawBuffer.EnemyLaneCache</c>（帧内 TGrid→敌方 lane 字典）值不值得再套一层池化分配/归还？
/// 本基准只测量，不改动生产代码。
///
/// 现状形状（照现状复刻）：
///   字典是 <c>DrawBuffer</c> 的字段 → 随 <c>DrawBuffer</c> 一起被 <c>bufferPool</c> 复用，每帧只 <c>Clear()</c> 一次（容量保留）；
///   只有 <c>CreateThreadLocalBuffer</c>（某线程首次用到）或 <c>ResetDrawResources</c>（Initialize/Dispose 清空池）之后，
///   才会重新 new 并重新增长。所以"是否还要池化"实际是在问三笔账：每帧的 Clear、每次缓冲创建的增长、
///   以及改成池化（自建字典栈池 / <c>ObjectPool.GetPooledDictionary</c>）后每帧的租还成本。
///
/// 量纲：`*_PerFrame` 是**每帧一份字典**的生命周期成本（<see cref="OperationsPerInvoke"/> 摊平到单帧）；
/// `ProductionTarget_*` 是**整帧**（含 lane 查询与并行区），两条之差 = 缓冲池被丢掉后重建的整帧代价。
/// </summary>
[MemoryDiagnoser]
public class ProjectileLaneCachePoolingBenchmarks
{
    /// <summary>每个并行工作线程在帧内看到的"不同 TGrid"数量 D（真实谱面 8090 在 0% 处整帧 D=2661，由 14 个工作线程分摊）。</summary>
    [Params(64, 512, 2661)]
    public int DistinctKeys;

    private const int FramesPerInvoke = 256;
    private const int BulletsPerFrame = 3500;

    private int[] keys = null!;
    private EnemyLaneStart lane = null!;
    private Bullet[] bullets = null!;
    private StubHost host = null!;
    private NullBulletTarget steadyTarget = null!;
    private NullBulletTarget coldTarget = null!;
    private Dictionary<int, EnemyLaneStart> reusable = null!;
    private readonly Stack<Dictionary<int, EnemyLaneStart>> pooled = new();

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkRuntime.EnsureInitialized();

        keys = new int[DistinctKeys];
        for (var i = 0; i < keys.Length; i++)
            keys[i] = i * 4;

        lane = new EnemyLaneStart { RecordId = 1, TGrid = TGrid.FromTotalGrid(0) };

        //帧负载：key 周期出现，于是每个工作线程看到的不同 TGrid 数 ≈ min(每分区弹丸数, D)
        bullets = new Bullet[BulletsPerFrame];
        for (var i = 0; i < bullets.Length; i++)
        {
            bullets[i] = new Bullet
            {
                TGrid = TGrid.FromTotalGrid(5000 + keys[i % keys.Length]),
                XGrid = new XGrid(0),
                Speed = 1f,
                TargetValue = Target.FixField,
                ShooterValue = Shooter.Enemy
            };
        }

        host = CreateHost(out var fumen);
        host.Editor._cacheSoflanGroupRecorder.SetDefault(fumen.SoflansMap.DefaultSoflanList);

        //稳态目标：Initialize 一次，缓冲池在两次测量之间保持预热
        steadyTarget = new NullBulletTarget();
        steadyTarget.Initialize(null!);
        _ = RenderFrame(steadyTarget);

        //冷路径目标：每次测量都 Initialize → ResetDrawResources 清空缓冲池
        coldTarget = new NullBulletTarget();

        reusable = new Dictionary<int, EnemyLaneStart>(DistinctKeys);
        Fill(reusable);

        PrintFacts();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        steadyTarget.Dispose();
        coldTarget.Dispose();
        host.Editor.EditorContext.Dispose();
        host.Editor.Setting.Dispose();
    }

    // =====================================================================
    // 字典生命周期：per-frame 形状（1 op = 一帧的一份字典）
    // =====================================================================

    /// <summary>现状形状：字典随 DrawBuffer 复用，每帧 Clear + 回填 D 条。</summary>
    [Benchmark(Baseline = true, OperationsPerInvoke = FramesPerInvoke)]
    public int Reused_ClearAndRefill_PerFrame()
    {
        for (var i = 0; i < FramesPerInvoke; i++)
        {
            reusable.Clear();
            Fill(reusable);
        }

        return reusable.Count;
    }

    /// <summary>
    /// 只回填、不 Clear（键集合固定，等于 D 次覆写）。仅用于把 Clear 的成本拆出来：
    /// 与上一条之差 ≈ 一帧 <c>Clear(D)</c> 的开销，也就是"池化分配/归还"能替换掉的那部分。
    /// </summary>
    [Benchmark(OperationsPerInvoke = FramesPerInvoke)]
    public int Reused_RefillOnly_PerFrame()
    {
        for (var i = 0; i < FramesPerInvoke; i++)
            Fill(reusable);

        return reusable.Count;
    }

    /// <summary>若改成每帧 new 一个字典（无容量提示，等价于现状字段初始化里的 <c>new()</c>）。</summary>
    [Benchmark(OperationsPerInvoke = FramesPerInvoke)]
    public int NewDictionary_NoCapacityHint_PerFrame()
    {
        var count = 0;
        for (var i = 0; i < FramesPerInvoke; i++)
        {
            var cache = new Dictionary<int, EnemyLaneStart>();
            Fill(cache);
            count = cache.Count;
        }

        return count;
    }

    /// <summary>若改成每帧 new 一个字典（带容量提示，是该形状的最好情况）。</summary>
    [Benchmark(OperationsPerInvoke = FramesPerInvoke)]
    public int NewDictionary_WithCapacityHint_PerFrame()
    {
        var count = 0;
        for (var i = 0; i < FramesPerInvoke; i++)
        {
            var cache = new Dictionary<int, EnemyLaneStart>(DistinctKeys);
            Fill(cache);
            count = cache.Count;
        }

        return count;
    }

    /// <summary>若改成仓库既有的 <see cref="ObjectPool"/> 池化字典（ArrayPool 支撑，租用即 Clear、归还原数组）。</summary>
    [Benchmark(OperationsPerInvoke = FramesPerInvoke)]
    public int PooledDictionary_RentFillReturn_PerFrame()
    {
        var count = 0;
        for (var i = 0; i < FramesPerInvoke; i++)
        {
            using var cache = ObjectPool.GetPooledDictionary<int, EnemyLaneStart>();
            Fill(cache);
            count = cache.Count;
        }

        return count;
    }

    /// <summary>若改成自建"只 Clear、不还数组"的字典栈池（BCL 字典，容量自留）。</summary>
    [Benchmark(OperationsPerInvoke = FramesPerInvoke)]
    public int OwnStackPool_RentFillReturn_PerFrame()
    {
        var count = 0;
        for (var i = 0; i < FramesPerInvoke; i++)
        {
            var cache = RentFromOwnPool();
            cache.Clear();
            Fill(cache);
            count = cache.Count;
            pooled.Push(cache);
        }

        return count;
    }

    // =====================================================================
    // 生产形状：整帧（含 lane 查询、几何、并行区）
    // =====================================================================

    /// <summary>稳态整帧：缓冲池预热、字典复用（现状每帧的样子）。</summary>
    [Benchmark]
    public int ProductionTarget_Frame_SteadyState() => RenderFrame(steadyTarget);

    /// <summary>
    /// 冷路径整帧：每帧都 <c>Initialize</c>（清空缓冲池）→ 每个工作线程重建 DrawBuffer 与其字典并在帧内增长。
    /// 与上一条之差 = "池被丢掉后重建" 的整帧上界（字典只是其中一部分）。
    /// </summary>
    [Benchmark]
    public int ProductionTarget_Frame_AfterPoolReset()
    {
        coldTarget.Initialize(null!);
        try
        {
            return RenderFrame(coldTarget);
        }
        finally
        {
            coldTarget.Dispose();
        }
    }

    // =====================================================================

    private int RenderFrame(NullBulletTarget target)
    {
        using var builder = new DrawCommandListBuilder();
        target.DrawBatch(host, builder, bullets);
        using var commandList = builder.GetDrawCommandList();
        return target.Drawn;
    }

    private void Fill(IDictionary<int, EnemyLaneStart> cache)
    {
        for (var i = 0; i < keys.Length; i++)
            cache[keys[i]] = lane;
    }

    private Dictionary<int, EnemyLaneStart> RentFromOwnPool()
    {
        while (pooled.TryPop(out var cache))
            return cache;

        return new Dictionary<int, EnemyLaneStart>(DistinctKeys);
    }

    private static StubHost CreateHost(out OngekiFumen fumen)
    {
        fumen = new OngekiFumen();
        for (var i = 0; i < 4; i++)
        {
            var laneStart = new EnemyLaneStart
            {
                RecordId = i + 1,
                TGrid = TGrid.FromTotalGrid(i * 1920 * 4),
                XGrid = new XGrid(i)
            };
            laneStart.AddChildObject(new EnemyLaneNext
            {
                TGrid = TGrid.FromTotalGrid(i * 1920 * 4 + 1920 * 8),
                XGrid = new XGrid(i + 4)
            });
            fumen.AddObject(laneStart);
        }

        var editor = new FumenVisualEditorViewModel
        {
            ViewWidth = 800,
            ViewHeight = 600,
            IsLocked = true, // 预览模式
            EditorContext = new EditorContext
            {
                ProjectData = new EditorProjectDataModel { AudioDuration = TimeSpan.FromSeconds(30) },
                Fumen = fumen
            }
        };
        editor.Setting.JudgeLineOffsetY = 0;
        editor.Setting.VerticalDisplayScale = 1;

        return new StubHost(editor)
        {
            CurrentDrawingTargetContext = new DrawingTargetContext
            {
                CurrentTGrid = TGrid.FromTotalUnit((float)(TGrid.FromTotalGrid(5000).TotalUnit - 450)),
                CurrentSoflanList = fumen.SoflansMap.DefaultSoflanList,
                ViewRelativeOriginY = 0,
                ViewRelativeRect = new VisibleRect(new(800, 0), new(0, 600))
            }
        };
    }

    private void PrintFacts()
    {
        var workerCount = Math.Max(2, Environment.ProcessorCount - 2);
        var perWorker = Math.Max(1, BulletsPerFrame / workerCount);
        Console.WriteLine($"[DistinctKeys={DistinctKeys}] 整帧 {BulletsPerFrame} 发弹丸（key 周期复用，每分区约 {perWorker} 发）" +
                          $" → 每个工作线程字典条目数 ≈ {Math.Min(perWorker, DistinctKeys)}，" +
                          $"ParallelCountLimit={EditorGlobalSetting.Default.ParallelCountLimit} DOP={workerCount}");
    }

    private sealed class NullBulletTarget : ProjectileBatchDrawTargetBase<Bullet>
    {
        private int drawn;

        public int Drawn => Volatile.Read(ref drawn);

        public override IEnumerable<string> DrawTargetID => [Bullet.CommandName];
        public override int DefaultRenderOrder => 0;

        public override void DrawVisibleObject_DesignMode(IFumenEditorDrawingContext target, Bullet obj, System.Numerics.Vector2 pos, float rotate, DrawBuffer buffer)
        {
        }

        public override void DrawVisibleObject_PreviewMode(IFumenEditorDrawingContext target, Bullet obj, System.Numerics.Vector2 pos, float rotate, DrawBuffer buffer)
            => Interlocked.Increment(ref drawn);
    }

    private sealed class StubHost(FumenVisualEditorViewModel editor) : IFumenEditorDrawingContext
    {
        public FumenVisualEditorViewModel Editor { get; } = editor;
        public DrawingTargetContext CurrentDrawingTargetContext { get; set; } = new();
        public TimeSpan CurrentPlayTime => TimeSpan.Zero;
        public IPerfomenceMonitor PerfomenceMonitor { get; } = new DummyPerformenceMonitor();
        public IRenderContext RenderContext => null!;

        public void RegisterSelectableObject(OngekiObjectBase obj, System.Numerics.Vector2 centerPos, System.Numerics.Vector2 size)
        {
        }

        public bool CheckDrawingVisible(DrawingVisible visible) => true;
        public bool CheckVisible(TGrid tGrid) => true;
        public bool CheckRangeVisible(TGrid minTGrid, TGrid maxTGrid) => true;
        public double ConvertToY(double tGridUnit, OngekiFumenEditor.Avalonia.Base.Collections.SoflanList soflans) => tGridUnit;
        public void Render(TimeSpan ts)
        {
        }
    }
}
