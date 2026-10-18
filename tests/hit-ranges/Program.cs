using System;
using System.Collections.Generic;
using System.Numerics;
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
        Console.WriteLine("判定范围几何回归通过。");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
