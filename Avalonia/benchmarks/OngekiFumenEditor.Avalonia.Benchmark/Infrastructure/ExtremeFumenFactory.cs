using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.EditorObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;

namespace OngekiFumenEditor.Avalonia.Benchmark.Infrastructure;

/// <summary>
/// 场景里的一个弹丸 + 它所属的 soflan 组（对应生产里 _cacheSoflanGroupRecorder 的查询结果）。
/// 生产里绘制目标是 <c>ProjectileBatchDrawTargetBase&lt;T&gt; where T : OngekiMovableObjectBase, IProjectile</c>，
/// 故这里同样同时持有两者：TGrid/XGrid 来自 <see cref="OngekiMovableObjectBase"/>，Speed 来自 <see cref="IProjectile"/>。
/// </summary>
public sealed class ProjectileEntry
{
    public required OngekiMovableObjectBase Object { get; init; }
    public required IProjectile Projectile { get; init; }
    public required int SoflanGroup { get; init; }

    public TGrid TGrid => Object.TGrid;
    public XGrid XGrid => Object.XGrid;

    /// <summary>生产 _Draw 里的 objSpeed；且本场景全部 Target != Player，故 IsEnableSoflan 恒为 true。</summary>
    public float Speed => Projectile.Speed;
}

/// <summary>
/// 用代码构造的极端谱面：不经过解析器，直接装配 <see cref="OngekiFumen"/>，
/// 并把 <c>_Draw</c> 用到的几何/映射参数（视口、判定线偏移、缩放、最慢弹速）显式带出来。
/// </summary>
public sealed class ExtremeFumen
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required OngekiFumen Fumen { get; init; }
    public required List<ProjectileEntry> Projectiles { get; init; }
    public required Dictionary<int, SoflanList> Groups { get; init; }

    /// <summary>VerticalDisplayScale（生产：editor.Setting.VerticalDisplayScale）。</summary>
    public double Scale { get; init; } = 1;

    /// <summary>视口高度 = rect.Height（生产：_Draw 里 appearOffsetTime = height / objSpeed）。</summary>
    public double ViewHeight { get; init; } = 900;

    public double RectMinY { get; init; }
    public double RectMaxY { get; init; } = 900;

    /// <summary>判定线相对视口顶的偏移（生产：baseY = min(rectMinY,rectMaxY) + JudgeLineOffsetY）。</summary>
    public double JudgeOffsetY { get; init; } = 200;

    public double BaseY => Math.Min(RectMinY, RectMaxY) + JudgeOffsetY;

    /// <summary>场景中最慢的弹速：收紧范围时的安全下限（spd → 0 时可见窗口 → ∞）。</summary>
    public double MinSpeed { get; init; } = 0.01;

    public TGrid MaxTGrid { get; init; } = TGrid.FromTotalGrid(1);
    public int FrameTGridCount { get; init; } = 129;

    public SoflanList GroupList(int group)
        => Groups.TryGetValue(group, out var list) ? list : Fumen.SoflansMap.DefaultSoflanList;

    /// <summary>生产：TGridCalculator.ConvertTGridUnitToY_PreviewMode(...)（预览模式 convertToY 的实际实现）。</summary>
    public double Y(TGrid t, int group)
        => TGridCalculator.ConvertTGridToY_PreviewMode(t, GroupList(group), Fumen.BpmList, Scale);

    public double YUnit(double unit, int group)
        => TGridCalculator.ConvertTGridUnitToY_PreviewMode(unit, GroupList(group), Fumen.BpmList, Scale);
}

/// <summary>
/// 极端场景谱面工厂。每个场景都在某个维度上刻意做绝，用来暴露「收紧预筛选范围」「缓存 lane 查询」
/// 这类改法在边缘条件下的失效：
///   uniform        基准：单组、speed=1、弹速 1..8
///   spd_min        弹速 0.01/0.02/0.05：appearOffsetTime = height/spd → 可见窗口放大 20–100 倍
///   spd_max        弹速 200/400：极端快（窗口极小，收紧范围的收益上限）
///   soflan_slow    soflan ×0.02..×0.1：时间被拉长 → 同样 Y 距离对应巨大 TGrid 差
///   soflan_fast    soflan ×50..×400：时间被压缩
///   soflan_reverse 倒车：负速段（Y 随 TGrid 递减）＋过零点 + InterpolatableSoflan 渐变
///   soflan_groups  4 个变速组，profile 从 ×0.05 到 ×400（同 TGrid 在不同组 Y 完全不同）
///   kitchen_sink   全叠加 + 同 TGrid 密集簇（D/K→1）+ 极端 BPM（1 / 20000）
/// </summary>
public static class ExtremeFumenFactory
{
    public static readonly string[] ScenarioNames =
    [
        "uniform", "spd_min", "spd_max", "soflan_slow", "soflan_fast", "soflan_reverse", "soflan_groups", "kitchen_sink"
    ];

    public static ExtremeFumen Build(string name) => name switch
    {
        "uniform" => Uniform(),
        "spd_min" => SpeedExtreme(0.01f, "慢弹速 0.01/0.02/0.05"),
        "spd_max" => SpeedExtreme(200f, "快弹速 200/400"),
        "soflan_slow" => SoflanExtreme(slow: true),
        "soflan_fast" => SoflanExtreme(slow: false),
        "soflan_reverse" => SoflanReverse(),
        "soflan_groups" => SoflanGroups(),
        "kitchen_sink" => KitchenSink(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "unknown extreme scenario"),
    };

    private sealed class Builder
    {
        public OngekiFumen Fumen { get; } = new();
        public List<ProjectileEntry> Projectiles { get; } = new();

        public Builder(double maxSectionCount = 400)
        {
            MaxTotalGrid = (int)(TGrid.DEFAULT_RES_T * 4 * maxSectionCount);
        }

        public int MaxTotalGrid { get; }

        public void AddSoflan(int group, double fromSection, double toSection, float speed)
        {
            var soflan = new Soflan
            {
                TGrid = TGrid.FromTotalGrid((int)(TGrid.DEFAULT_RES_T * 4 * fromSection)),
                Speed = speed,
                SoflanGroup = group,
            };
            soflan.EndTGrid = TGrid.FromTotalGrid((int)(TGrid.DEFAULT_RES_T * 4 * toSection));
            Fumen.SoflansMap.Add(soflan);
        }

        /// <summary>渐变变速（含倒车：fromSpeed/toSpeed 可含负值）。</summary>
        public void AddRampSoflan(int group, double fromSection, double toSection, float fromSpeed, float toSpeed)
        {
            var soflan = new InterpolatableSoflan
            {
                TGrid = TGrid.FromTotalGrid((int)(TGrid.DEFAULT_RES_T * 4 * fromSection)),
                Speed = fromSpeed,
                SoflanGroup = group,
                InterpolateCountPerResT = 8,
            };
            soflan.EndTGrid = TGrid.FromTotalGrid((int)(TGrid.DEFAULT_RES_T * 4 * toSection));
            ((InterpolatableSoflan.InterpolatableSoflanIndicator)soflan.EndIndicator).Speed = toSpeed;
            Fumen.SoflansMap.Add(soflan);
        }

        public void AddBpm(double section, double bpm)
            => Fumen.BpmList.Add(new BPMChange { TGrid = TGrid.FromTotalGrid((int)(TGrid.DEFAULT_RES_T * 4 * section)), BPM = bpm });

        public void AddLanes(int count, int group = 0)
        {
            for (var i = 0; i < count; i++)
                Fumen.Lanes.Add(new EnemyLaneStart
                {
                    RecordId = i,
                    TGrid = TGrid.FromTotalGrid((int)((double)MaxTotalGrid * i / Math.Max(1, count))),
                });
        }

        /// <summary>在 [fromSection, toSection] 上放 <paramref name="count"/> 个弹丸（speed 在 speeds 间轮转）。</summary>
        public void AddProjectiles(double fromSection, double toSection, int count, float[] speeds, int group = 0,
            Shooter shooter = Shooter.Enemy, int clusterSize = 1, int beltCount = 0)
        {
            var fromGrid = (int)(TGrid.DEFAULT_RES_T * 4 * fromSection);
            var toGrid = (int)(TGrid.DEFAULT_RES_T * 4 * toSection);
            var span = Math.Max(1, toGrid - fromGrid);

            for (var i = 0; i < count; i++)
            {
                var cluster = i / Math.Max(1, clusterSize);
                var unitIndex = cluster * Math.Max(1, clusterSize);
                var totalGrid = Math.Min(toGrid, fromGrid + (int)((long)span * unitIndex / Math.Max(1, count)));
                var tGrid = TGrid.FromTotalGrid(totalGrid);
                var speed = speeds[i % speeds.Length];

                if (beltCount > 0 && i % Math.Max(1, count / Math.Max(1, beltCount)) == 0)
                {
                    var bell = new Bell
                    {
                        TGrid = tGrid,
                        XGrid = new XGrid(i % 12 - 6, 0),
                        Speed = speed,
                        TargetValue = Target.FixField,
                        ShooterValue = shooter,
                    };
                    Fumen.Bells.Add(bell);
                    Projectiles.Add(new ProjectileEntry { Object = bell, Projectile = bell, SoflanGroup = group });
                }
                else
                {
                    var bullet = new Bullet
                    {
                        TGrid = tGrid,
                        XGrid = new XGrid(i % 12 - 6, 0),
                        Speed = speed,
                        TargetValue = Target.FixField,
                        ShooterValue = shooter,
                    };
                    Fumen.Bullets.Add(bullet);
                    Projectiles.Add(new ProjectileEntry { Object = bullet, Projectile = bullet, SoflanGroup = group });
                }
            }
        }

        public void AddSameTGridCluster(double section, int count, float speed, int group = 0, Shooter shooter = Shooter.Enemy)
        {
            var tGrid = TGrid.FromTotalGrid((int)(TGrid.DEFAULT_RES_T * 4 * section));
            for (var i = 0; i < count; i++)
            {
                var bullet = new Bullet
                {
                    TGrid = tGrid,
                    XGrid = new XGrid(i % 24 - 12, 0),
                    Speed = speed,
                    TargetValue = Target.FixField,
                    ShooterValue = shooter,
                };
                Fumen.Bullets.Add(bullet);
                Projectiles.Add(new ProjectileEntry { Object = bullet, Projectile = bullet, SoflanGroup = group });
            }
        }

        public ExtremeFumen Finish(string name, string desc, double minSpeed)
            => new()
            {
                Name = name,
                Description = desc,
                Fumen = Fumen,
                Projectiles = Projectiles,
                Groups = Fumen.SoflansMap.ToDictionary(x => x.Key, x => x.Value),
                MaxTGrid = TGrid.FromTotalGrid(MaxTotalGrid),
                MinSpeed = minSpeed,
            };
    }

    // ------------------------------------------------------------------ 场景

    private static ExtremeFumen Uniform()
    {
        var b = new Builder(120);
        b.AddSoflan(0, 0, 400, 1);
        b.AddLanes(200);
        b.AddProjectiles(0, 120, 3000, [1, 2, 4, 8], clusterSize: 4, beltCount: 24);
        return b.Finish("uniform", "基准：单组 speed=1，弹速 1..8", 1);
    }

    private static ExtremeFumen SpeedExtreme(float baseSpeed, string desc)
    {
        var b = new Builder(120);
        b.AddSoflan(0, 0, 400, 1);
        b.AddLanes(200);
        b.AddProjectiles(0, 120, 3000,
            [baseSpeed, baseSpeed * 2, baseSpeed * 1.5f, baseSpeed * 0.5f], clusterSize: 3, beltCount: 20);
        return b.Finish(desc.Contains("慢") ? "spd_min" : "spd_max", desc, baseSpeed * 0.5f);
    }

    private static ExtremeFumen SoflanExtreme(bool slow)
    {
        var b = new Builder(200);
        var speeds = slow ? new[] { 0.02f, 0.05f, 0.1f } : new[] { 50f, 150f, 400f };
        for (var i = 0; i < 8; i++)
            b.AddSoflan(0, i * 25, (i + 1) * 25, speeds[i % speeds.Length]);
        b.AddLanes(200);
        b.AddProjectiles(0, 200, 4000, [1, 2, 6], clusterSize: 4, beltCount: 24);
        return b.Finish(slow ? "soflan_slow" : "soflan_fast",
            slow ? "soflan ×0.02..×0.1（时间拉伸）" : "soflan ×50..×400（时间压缩）", 1);
    }

    private static ExtremeFumen SoflanReverse()
    {
        var b = new Builder(200);
        // 正 → 0 → 负（倒车）→ 0 → 正，另加两段渐变（InterpolatableSoflan）
        b.AddSoflan(0, 0, 20, 1);
        b.AddRampSoflan(0, 20, 40, 1, -1);          // 逐渐减速到 -1（倒车）
        b.AddSoflan(0, 40, 60, -1);                 // 倒车段：Y 随 TGrid 递减
        b.AddRampSoflan(0, 60, 80, -1, 1);          // 回正
        b.AddSoflan(0, 80, 100, 0.0f);              // 完全静止（Y 恒定：TGrid 变了 Y 不变）
        b.AddSoflan(0, 100, 120, 1);
        b.AddRampSoflan(0, 120, 140, 1, -0.02f);
        b.AddSoflan(0, 140, 200, -0.02f);
        b.AddLanes(200);
        b.AddProjectiles(0, 200, 4000, [1, 2, 6], clusterSize: 4, beltCount: 24);
        return b.Finish("soflan_reverse", "倒车：负速段 + 过零 + 静止段 + 渐变", 1);
    }

    private static ExtremeFumen SoflanGroups()
    {
        var b = new Builder(200);
        var profiles = new[] { 0.05f, 1f, 20f, 400f };
        for (var g = 0; g < 4; g++)
            for (var i = 0; i < 4; i++)
                b.AddSoflan(g, i * 50, (i + 1) * 50, profiles[g]);
        b.AddLanes(200);
        // 四个组各放一批弹丸：同 TGrid 在不同组 Y 完全不同
        for (var g = 0; g < 4; g++)
            b.AddProjectiles(0, 200, 1000, [1, 2, 6], group: g, clusterSize: 4, beltCount: 6);
        return b.Finish("soflan_groups", "4 个变速组（×0.05 / ×1 / ×20 / ×400）", 1);
    }

    private static ExtremeFumen KitchenSink()
    {
        var b = new Builder(300);
        // 极端 BPM
        b.AddBpm(0, 1);
        b.AddBpm(50, 20000);
        b.AddBpm(100, 30);
        b.AddBpm(150, 1);
        b.AddBpm(200, 600);

        // 5 个组：极慢 / 极快 / 倒车 / 静止 / 剧烈渐变
        for (var i = 0; i < 6; i++)
        {
            b.AddSoflan(0, i * 50, (i + 1) * 50, 0.02f);
            b.AddSoflan(1, i * 50, (i + 1) * 50, 400f);
            b.AddSoflan(2, i * 50, (i + 1) * 50, i % 2 == 0 ? -1f : 1f);
            b.AddSoflan(3, i * 50, (i + 1) * 50, 0f);
            b.AddRampSoflan(4, i * 50, (i + 1) * 50, 1f, -0.5f);
        }
        b.AddLanes(256);

        // 极慢 + 极快弹速混合、密集簇、bell 群
        for (var g = 0; g < 5; g++)
        {
            b.AddProjectiles(0, 300, 1500,
                g switch
                {
                    0 => [0.01f, 0.02f],
                    1 => [200f, 400f],
                    2 => [1f, 2f],
                    3 => [0.5f, 1f],
                    _ => [4f, 8f],
                }, group: g, clusterSize: 5, beltCount: 10);
        }

        // 同 TGrid 密集簇（D/K → 1）+ 正前方簇（收紧范围收益上限）
        b.AddSameTGridCluster(10, 400, 0.01f, group: 0, shooter: Shooter.Enemy);
        b.AddSameTGridCluster(20, 400, 400f, group: 1, shooter: Shooter.TargetHead);
        b.AddSameTGridCluster(299.9, 400, 0.01f, group: 4, shooter: Shooter.Center);

        return b.Finish("kitchen_sink", "全叠加：极端 BPM + 5 组极端变速 + 极慢/极快弹速 + 同 TGrid 密集簇", 0.01f);
    }
}
