#nullable enable

using System.Collections.Concurrent;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;
using OngekiFumenEditor.Avalonia.Models.Settings;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.BulletBell;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using Avalonia.Headless.XUnit;
using Xunit;
using NumericsVector2 = System.Numerics.Vector2;
using OpenTkVector2 = OpenTK.Mathematics.Vector2;

namespace OngekiFumenEditor.Avalonia.Tests.Graphics;

/// <summary>
/// RND-008 改法 2（帧内 TGrid→敌方 lane 缓存 + 并行 miss）的验收：
///   ① 缓存解析结果必须与改造前的逐项查询表达式逐一致（含"多条 lane 同时命中取最后一个"的语义）；
///   ② 命中缓存不得改变任何一枚弹丸的落点，串行/并行两种形状必须给出同一组坐标；
///   ③ 帧内缓存不得跨帧存活（池化缓冲被复用时，上一帧的 lane 解析不能漏到下一帧）。
/// 断言全部落在可观测输出（本帧实际画出的坐标）上，不检查实现细节。
/// </summary>
public sealed class ProjectileBatchLaneCacheTests
{
    private const float ViewWidth = 800;
    private const float ViewHeight = 600;

    /// <summary>弹丸 TGrid（grid）：5000 grids ≈ 2.604 拍。</summary>
    private static readonly TGrid BankTGrid = TGrid.FromTotalGrid(5000);

    [AvaloniaFact]
    public void QueryEnemyLane_MatchesThePreviousPerItemLinqExpression()
    {
        var fumen = CreateFumen();

        for (var totalGrid = 0; totalGrid <= 20_000; totalGrid += 7)
        {
            var probe = TGrid.FromTotalGrid(totalGrid);
            var expected = fumen.Lanes.GetVisibleStartObjects(probe, probe)
                .OfType<EnemyLaneStart>()
                .LastOrDefault();
            var actual = ProjectileBatchDrawTargetBase<Bullet>.QueryEnemyLane(fumen, probe);

            Assert.Same(expected, actual);
        }
    }

    [AvaloniaFact]
    public void SameTGridBullets_ResolveTheLaneOnce_AndDrawIdentically()
    {
        var target = CreateTarget();
        var bullets = Enumerable.Range(0, 6).Select(i => CreateEnemyBullet(BankTGrid)).ToArray();

        var sharedFrame = Render(target, CreateFumen(), bullets);
        var soloFrame = Render(target, CreateFumen(), [bullets[0]]);

        Assert.Equal(bullets.Length, sharedFrame.Count);
        //同帧内的 6 发弹丸与"整帧只有它自己"时的落点完全一致：命中缓存不改变结果
        Assert.All(bullets, bullet => Assert.Equal(soloFrame[bullets[0]], sharedFrame[bullet]));

        //lane 确实参与落点计算：同一枚弹丸在没有 lane 的谱面里画在别的位置
        var noLaneFrame = Render(target, new OngekiFumen(), bullets);
        Assert.NotEqual(sharedFrame[bullets[0]], noLaneFrame[bullets[0]]);
    }

    [AvaloniaFact]
    public void WhenSeveralEnemyLanesMatch_TheLaneLastOrDefaultWouldPickWins()
    {
        var fumen = CreateFumen();
        var bullet = CreateEnemyBullet(BankTGrid);

        //旧表达式的判定结果（这就是被对比的规格）
        var winner = fumen.Lanes.GetVisibleStartObjects(BankTGrid, BankTGrid)
            .OfType<EnemyLaneStart>()
            .LastOrDefault();
        var loser = fumen.Lanes.GetVisibleStartObjects(BankTGrid, BankTGrid)
            .OfType<EnemyLaneStart>()
            .First();

        Assert.NotNull(winner);
        Assert.NotSame(winner, loser);

        var target = CreateTarget();
        var allLanes = Render(target, fumen, [bullet]);
        var winnerOnly = Render(target, FumenWith(winner!), [bullet]);
        var loserOnly = Render(target, FumenWith(loser), [bullet]);

        Assert.Equal(winnerOnly[bullet], allLanes[bullet]);
        Assert.NotEqual(loserOnly[bullet], allLanes[bullet]);
    }

    [AvaloniaFact]
    public void ParallelAndSequentialShapes_DrawTheSameCoordinates()
    {
        var limit = EditorGlobalSetting.Default.ParallelCountLimit;
        var fumen = CreateFumen();

        //超过阈值 → 走 Parallel.ForEach（每个工作线程一份缓存、miss 也在区内发生）
        var bullets = Enumerable.Range(0, limit + 500)
            .Select(i => CreateEnemyBullet(TGrid.FromTotalGrid(5000 + i % 16)))
            .ToArray();

        var parallelTarget = CreateTarget();
        var parallelFrame = Render(parallelTarget, fumen, bullets);

        var sequentialFrame = Render(CreateTarget(), CreateFumen(), bullets.Take(32).ToArray());

        Assert.Equal(bullets.Length, parallelFrame.Count);
        Assert.All(sequentialFrame, pair => Assert.Equal(pair.Value, parallelFrame[pair.Key]));
    }

    [AvaloniaFact]
    public void FrameLocalCache_DoesNotLeakLaneResolutionIntoTheNextFrame()
    {
        var bulletCounts = new[] { 4, EditorGlobalSetting.Default.ParallelCountLimit + 500 };

        foreach (var bulletCount in bulletCounts)
        {
            var target = CreateTarget();
            var bullets = Enumerable.Range(0, bulletCount)
                .Select(i => CreateEnemyBullet(BankTGrid))
                .ToArray();

            //第一帧：lane 终点 X = 4；第二帧（同一实例、复用池化缓冲）：lane 终点 X = -2
            var before = Render(target, FumenWith(CreateLane(1, 0, 0, 7680, 4)), bullets);
            var after = Render(target, FumenWith(CreateLane(2, 0, 0, 7680, -2)), bullets);
            var fresh = Render(CreateTarget(), FumenWith(CreateLane(2, 0, 0, 7680, -2)), bullets);

            Assert.All(bullets, bullet =>
            {
                Assert.Equal(fresh[bullet], after[bullet]);
                Assert.NotEqual(before[bullet], after[bullet]);
            });
        }
    }

    // =====================================================================

    private static OngekiFumen CreateFumen()
    {
        //两条敌方 lane：一条到 4 拍（X 走到 4），一条到 10000 grids（X 走到 -2）。
        //再加一条同样覆盖该区间、但非敌方类型的 lane，用来确认 OfType 过滤没被绕开。
        return FumenWith(
            CreateLane(1, 0, 0, 7680, 4),
            CreateLane(2, 0, 0, 10_000, -2),
            new LaneLeftStart { RecordId = 3, TGrid = TGrid.FromTotalGrid(0), XGrid = new XGrid(0) });
    }

    private static OngekiFumen FumenWith(params LaneStartBase[] lanes)
    {
        var fumen = new OngekiFumen();
        foreach (var lane in lanes)
            fumen.AddObject(lane);
        return fumen;
    }

    private static EnemyLaneStart CreateLane(int recordId, int startGrid, float startXUnit, int endGrid, float endXUnit)
    {
        var lane = new EnemyLaneStart
        {
            RecordId = recordId,
            TGrid = TGrid.FromTotalGrid(startGrid),
            XGrid = new XGrid(startXUnit)
        };
        lane.AddChildObject(new EnemyLaneNext
        {
            TGrid = TGrid.FromTotalGrid(endGrid),
            XGrid = new XGrid(endXUnit)
        });
        return lane;
    }

    /// <summary>
    /// 敌方弹丸：弹速 1、目标 FixField，故 appearOffsetTime = height/spd = 600 拍，
    /// 播放位置取弹丸前 450 拍 → precent = 0.25、timeY = 450，落点横坐标 = fromX + 0.25*(toX - fromX)。
    /// </summary>
    private static Bullet CreateEnemyBullet(TGrid tGrid) => new()
    {
        TGrid = tGrid.CopyNew(),
        XGrid = new XGrid(0),
        Speed = 1f,
        TargetValue = Target.FixField,
        ShooterValue = Shooter.Enemy
    };

    private static TGrid PlayheadTGrid => TGrid.FromTotalUnit((float)(BankTGrid.TotalUnit - 450));

    private static RecordingBulletTarget CreateTarget()
    {
        var target = new RecordingBulletTarget();
        target.Initialize(null!);
        return target;
    }

    /// <summary>画一帧（一次 DrawBatch），返回每枚被画出的弹丸的横坐标。</summary>
    private static IReadOnlyDictionary<Bullet, float> Render(
        RecordingBulletTarget target,
        OngekiFumen fumen,
        IReadOnlyList<Bullet> bullets)
    {
        var host = CreateHost(fumen);
        try
        {
            target.Reset();
            using var builder = new DrawCommandListBuilder();
            target.DrawBatch(host, builder, bullets);
            using var commandList = builder.GetDrawCommandList();
        }
        finally
        {
            host.Editor.EditorContext.Dispose();
            host.Editor.Setting.Dispose();
        }

        return bullets.ToDictionary(bullet => bullet, bullet => target.Drawn[bullet]);
    }

    private static StubDrawingContext CreateHost(OngekiFumen fumen)
    {
        var editor = new FumenVisualEditorViewModel
        {
            ViewWidth = ViewWidth,
            ViewHeight = ViewHeight,
            IsLocked = true, // 锁定 = 预览模式
            EditorContext = new EditorContext
            {
                ProjectData = new EditorProjectDataModel { AudioDuration = TimeSpan.FromSeconds(30) },
                Fumen = fumen
            }
        };
        editor.Setting.JudgeLineOffsetY = 0;
        editor.Setting.VerticalDisplayScale = 1;

        return new StubDrawingContext(editor)
        {
            CurrentDrawingTargetContext = new DrawingTargetContext
            {
                CurrentTGrid = PlayheadTGrid,
                CurrentSoflanList = fumen.SoflansMap.DefaultSoflanList,
                ViewRelativeOriginY = 0,
                ViewRelativeRect = new VisibleRect(new OpenTkVector2(ViewWidth, 0), new OpenTkVector2(0, ViewHeight))
            }
        };
    }

    private sealed class RecordingBulletTarget : ProjectileBatchDrawTargetBase<Bullet>
    {
        private readonly ConcurrentDictionary<Bullet, float> drawn = new();

        public override IEnumerable<string> DrawTargetID => [Bullet.CommandName];
        public override int DefaultRenderOrder => 0;

        public IReadOnlyDictionary<Bullet, float> Drawn => drawn;

        public void Reset() => drawn.Clear();

        public override void DrawVisibleObject_DesignMode(IFumenEditorDrawingContext target, Bullet obj, NumericsVector2 pos, float rotate, DrawBuffer buffer)
        {
        }

        public override void DrawVisibleObject_PreviewMode(IFumenEditorDrawingContext target, Bullet obj, NumericsVector2 pos, float rotate, DrawBuffer buffer)
        {
            drawn[obj] = pos.X;
        }
    }

    private sealed class StubDrawingContext(FumenVisualEditorViewModel editor) : IFumenEditorDrawingContext
    {
        public FumenVisualEditorViewModel Editor { get; } = editor;
        public DrawingTargetContext CurrentDrawingTargetContext { get; set; } = new();
        public TimeSpan CurrentPlayTime => TimeSpan.Zero;
        public IPerfomenceMonitor PerfomenceMonitor { get; } = new DummyPerformenceMonitor();
        public IRenderContext RenderContext => null!;

        public void RegisterSelectableObject(OngekiObjectBase obj, NumericsVector2 centerPos, NumericsVector2 size)
        {
        }

        public bool CheckDrawingVisible(DrawingVisible visible) => true;
        public bool CheckVisible(TGrid tGrid) => true;
        public bool CheckRangeVisible(TGrid minTGrid, TGrid maxTGrid) => true;

        /// <summary>Y 映射直接用 TGrid 单位，于是可见窗口在测试里可以手算。</summary>
        public double ConvertToY(double tGridUnit, OngekiFumenEditor.Avalonia.Base.Collections.SoflanList soflans) => tGridUnit;

        public void Render(TimeSpan ts)
        {
        }
    }
}
