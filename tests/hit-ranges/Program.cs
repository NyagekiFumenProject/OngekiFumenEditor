using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

internal static class Program
{
    static void Main()
    {
        Assert(HitRangeGeometry.BulletHalfWidth(BulletType.Circle) == 2, "圆弹判定半宽应为2。");
        Assert(HitRangeGeometry.BulletHalfWidth(BulletType.Needle) == 1 && HitRangeGeometry.BulletHalfWidth(BulletType.Square) == 1, "针弹和方弹判定半宽应为1。");
        var band = new List<PolygonVertex>();
        HitRangeGeometry.AddBand(band, new Vector2(0, -20), new Vector2(0, 20), 3, -5, 5, Vector4.One);
        Assert(band.Count == 6, "跨越可视区的激光段必须绘制裁剪后的矩形。");
        band.Clear();
        HitRangeGeometry.AddBand(band, new Vector2(0, 10), new Vector2(0, 20), 3, -5, 5, Vector4.One);
        Assert(band.Count == 0, "屏幕外激光段不应生成几何。");
        var path = new List<LineVertex>
        {
            new(new(0, 0), Vector4.One, VertexDash.Solider),
            new(new(0, 1), Vector4.Zero, VertexDash.Solider),
            new(new(0, 2), Vector4.One, VertexDash.Solider)
        };
        HitRangeGeometry.AddVisibleBands(band, path, 3, -5, 5, Vector4.One);
        Assert(band.Count == 0, "透明顶点必须保持断段。");
        path[1] = new(new(0, 1), Vector4.One, new VertexDash(0, 0));
        HitRangeGeometry.AddVisibleBands(band, path, 3, -5, 5, Vector4.One);
        Assert(band.Count == 0, "无效路径标记必须保持断段。");
        var target = ProjectileTargetGeometry.Resolve(500, Target.FixField, 0, TimeSpan.Zero, 10, 517, 0, _ => throw new InvalidOperationException("固定目标不应读取玩家位置。"));
        Assert(target.LandingXUnit == 517 && target.EnvelopeMinXUnit == 517 && target.EnvelopeMaxXUnit == 517, "固定目标及零随机范围应给出精确落点。");
        var player = ProjectileTargetGeometry.Resolve(500, Target.Player, 2, TimeSpan.Zero, 10, 17, 0, time =>
        {
            Assert(time == TimeSpan.Zero, "速度调整后的玩家历史时间应钳制到零。");
            return 30;
        });
        Assert(player.LandingXUnit == 47, "玩家目标应包含历史位置和物件 X。");
        foreach (var speed in new[] { .5f, 2f, .00001f })
        {
            var queried = false;
            var historical = ProjectileTargetGeometry.Resolve(500, Target.Player, speed, TimeSpan.FromSeconds(10), 10, 17, 0, time =>
            {
                queried = true;
                var expected = TimeSpan.FromSeconds(10) - OngekiFumenEditor.Modules.FumenVisualEditor.TGridCalculator.ConvertFrameToAudioTime((40f - 7.5f) / (.47f * MathF.Min(speed, 1)));
                if (expected < TimeSpan.Zero)
                    expected = TimeSpan.Zero;
                Assert(time == expected, "非零音频时间应按原正速度公式查询玩家历史位置。");
                return 30;
            });
            Assert(queried && historical.LandingXUnit == 47, "不同正速度应保留玩家历史位置语义。");
        }
        var randomA = ProjectileTargetGeometry.Resolve(500, Target.FixField, 1, TimeSpan.Zero, 10, 517, 23, _ => 0);
        var randomB = ProjectileTargetGeometry.Resolve(500, Target.FixField, 1, TimeSpan.Zero, 10, 517, 23, _ => 0);
        Assert(randomA.LandingXUnit == randomB.LandingXUnit && randomA.LandingXUnit >= 494 && randomA.LandingXUnit <= 540, "随机落点须可重现且处于范围内。");
        Assert(randomA.EnvelopeMinXUnit == 494 && randomA.EnvelopeMaxXUnit == 540, "随机包络必须保留完整偏移边界。");
        var stopped = ProjectileTargetGeometry.Resolve(500, Target.Player, 0, TimeSpan.FromSeconds(10), 10, 17, 0, time =>
        {
            Assert(time == TimeSpan.FromSeconds(10), "零速玩家目标应在命中帧读取玩家位置。");
            return 30;
        });
        Assert(stopped.LandingXUnit == 47, "零速下的玩家目标须使用命中帧玩家位置。");
        var negative = ProjectileTargetGeometry.Resolve(500, Target.Player, -2f, TimeSpan.FromSeconds(10), 10, 17, 0, time =>
        {
            var expected = TimeSpan.FromSeconds(10) - TGridCalculator.ConvertFrameToAudioTime((40f - 7.5f) / (.47f * MathF.Min(-2f, 1f)));
            Assert(time == expected, "负速玩家目标应按游戏公式在命中后读取玩家位置。");
            return 30;
        });
        Assert(negative.LandingXUnit == 47, "负速玩家目标须保留玩家位置。");
        var slowest = ProjectileTargetGeometry.Resolve(500, Target.Player, float.Epsilon, TimeSpan.FromSeconds(10), 10, 17, 0, time =>
        {
            Assert(time == TimeSpan.Zero, "极小正速度应钳制历史查询时间，不能溢出。");
            return 30;
        });
        Assert(slowest.LandingXUnit == 47, "极小正速度须保留安全落点。");
        var extreme = ProjectileTargetGeometry.Resolve(int.MinValue, Target.FixField, 0, TimeSpan.Zero, int.MaxValue, 0, int.MaxValue, _ => 0);
        Assert(double.IsFinite(extreme.LandingXUnit) && extreme.EnvelopeMinXUnit == -int.MaxValue && extreme.EnvelopeMaxXUnit == int.MaxValue, "随机极值必须保持有限且不溢出。");
        TestDrawingTargets();
        Console.WriteLine("判定范围几何回归通过。");
    }

    static void TestDrawingTargets()
    {
        var editor = (FumenVisualEditorViewModel)RuntimeHelpers.GetUninitializedObject(typeof(FumenVisualEditorViewModel));
        SetField(editor, "editorProjectData", new EditorProjectDataModel());
        SetField(editor, "<Setting>k__BackingField", new EditorSetting());
        SetField(editor, "<PlayerLocationRecorder>k__BackingField", new PlayerLocationRecorder());
        SetField(editor, "viewWidth", 800f);
        OngekiFumenEditor.Properties.EditorGlobalSetting.Default.ShowBulletHitRanges = true;
        OngekiFumenEditor.Properties.EditorGlobalSetting.Default.ShowBeamHitRanges = true;
        var context = DispatchProxy.Create<IFumenEditorDrawingContext, DrawingContextProxy>();
        ((DrawingContextProxy)(object)context).Editor = editor;
        var bullet = new Bullet { Id = 10, XGrid = new XGrid(3), TGrid = new TGrid(1), RandomOffsetRange = 5 };
        var expected = ProjectileTargetGeometry.Resolve(BulletPallete.RandomSeed, bullet.TargetValue, bullet.Speed,
            TGridCalculator.ConvertTGridToAudioTime(bullet.TGrid, editor.Fumen.BpmList), bullet.Id, 3, 5, _ => 0);
        using var builder = new DrawCommandListBuilder();
        new BulletHitRangeDrawingTarget().DrawBatch(context, builder, new[] { bullet });
        using (var commands = builder.GetDrawCommandList())
        {
            var polygon = commands.Commands.OfType<DrawPolygonCommand>().Single();
            Assert(Math.Abs(polygon.Vertices.Min(v => v.Point.X) - XGridCalculator.ConvertXGridToX(expected.LandingXUnit - 2, editor)) < .001, "实际弹幕绘制须使用共享落点和实体半宽。");
            var outline = commands.Commands.OfType<DrawSimpleLinesCommand>().Single();
            Assert(outline.Points.Any(v => v.Dash == new VertexDash(4, 3)), "随机包络必须使用虚线。");
            Assert(Math.Abs(outline.Points.Min(v => v.Point.X) - XGridCalculator.ConvertXGridToX(-4, editor)) < .001, "随机包络须覆盖负偏移及实体半宽。");
        }
        editor.PlayerLocationRecorder.Commit(TimeSpan.Zero, 2);
        editor.PlayerLocationRecorder.Commit(TimeSpan.FromSeconds(30), 8);
        bullet.TargetValue = Target.Player;
        bullet.Speed = .5f;
        bullet.TGrid = new TGrid(8);
        var playerExpected = ProjectileTargetGeometry.Resolve(BulletPallete.RandomSeed, Target.Player, .5f,
            TGridCalculator.ConvertTGridToAudioTime(bullet.TGrid, editor.Fumen.BpmList), bullet.Id, 3, 5, editor.PlayerLocationRecorder.GetLocationXUnit);
        new BulletHitRangeDrawingTarget().DrawBatch(context, builder, new[] { bullet });
        using (var commands = builder.GetDrawCommandList())
        {
            var polygon = commands.Commands.OfType<DrawPolygonCommand>().Single();
            Assert(Math.Abs(polygon.Vertices.Min(v => v.Point.X) - XGridCalculator.ConvertXGridToX(playerExpected.LandingXUnit - 2, editor)) < .001, "Player 实际命中带须包含历史玩家位置。");
        }
        var beam = new BeamStart { XGrid = new XGrid(0), TGrid = new TGrid(1), WidthId = new WidthId(999, 100, 8) };
        beam.AddChildObject(new BeamNext { XGrid = new XGrid(0), TGrid = new TGrid(2) });
        new BeamHitRangeDrawingTarget().DrawBatch(context, builder, new[] { beam });
        using (var commands = builder.GetDrawCommandList())
        {
            var polygon = commands.Commands.OfType<DrawPolygonCommand>().Single();
            var expectedWidth = XGridCalculator.ConvertXGridToX(4d, editor) - XGridCalculator.ConvertXGridToX(-4d, editor);
            Assert(Math.Abs(polygon.Vertices.Max(v => v.Point.X) - polygon.Vertices.Min(v => v.Point.X) - expectedWidth) < .001, "Beam 绘制须取起点 WidthDraw/2（游戏 widthJudge 与 widthDraw 同值），不能取编辑器 WidthJudge。");
        }
    }

    static void SetField(object obj, string name, object value) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(obj, value);

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

public class DrawingContextProxy : DispatchProxy
{
    public FumenVisualEditorViewModel Editor = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        switch (method!.Name)
        {
            case "get_Editor": return Editor;
            case "get_CurrentDrawingTargetContext": return new DrawingTargetContext { ViewRelativeRect = new VisibleRect(new Vector2(800, 0), new Vector2(0, 100)) };
            case "CheckVisible":
            case "CheckRangeVisible": return true;
            case "ConvertToViewRelativeY_DefaultSoflanGroup": return ((TGrid)args![0]!).TotalUnit * 10;
            case "ConvertToViewRelativeY": return args![0] is TGrid grid ? grid.TotalUnit * 10 : (double)args[0]! * 10;
            default: throw new InvalidOperationException("未预期的绘图上下文调用：" + method.Name);
        }
    }
}
