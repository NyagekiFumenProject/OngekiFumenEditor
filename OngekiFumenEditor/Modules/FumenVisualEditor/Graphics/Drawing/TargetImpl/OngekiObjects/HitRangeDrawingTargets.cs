using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Numerics;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using OngekiFumenEditor.Utils;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;
namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects
{
    public static class HitRangeGeometry
    {
        // 本机游戏 ShellNoteCore.setShellWidth：圆弹全宽 4，针/方弹全宽 2；大小只改变外观。
        public static float BulletHalfWidth(BulletType type) => type == BulletType.Circle ? 2 : 1;
        public static Vector4 BulletColor(BulletDamageType damage) => damage switch
        {
            BulletDamageType.Hard => new Vector4(84 / 255f, 0, 169 / 255f, .48f),
            BulletDamageType.Danger => new Vector4(15 / 255f, 1 / 255f, 33 / 255f, .65f),
            _ => new Vector4(251 / 255f, 7 / 255f, 178 / 255f, .35f)
        };
        public static void AddQuad(IList<PolygonVertex> output, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Vector4 color)
        {
            output.Add(new(a, color));
            output.Add(new(b, color));
            output.Add(new(c, color));
            output.Add(new(c, color));
            output.Add(new(b, color));
            output.Add(new(d, color));
        }
        public static void AddBand(IList<PolygonVertex> output, Vector2 a, Vector2 b, float halfWidth, float minY, float maxY, Vector4 color)
        {
            if (!float.IsFinite(a.X) || !float.IsFinite(a.Y) || !float.IsFinite(b.X) || !float.IsFinite(b.Y) || halfWidth <= 0) return;
            if (Math.Max(a.Y, b.Y) < minY || Math.Min(a.Y, b.Y) > maxY) return;
            if (Math.Abs(b.Y - a.Y) < .0001f)
            {
                var left = Math.Min(a.X, b.X) - halfWidth;
                var right = Math.Max(a.X, b.X) + halfWidth;
                var low = Math.Max(minY, a.Y - 1);
                var high = Math.Min(maxY, a.Y + 1);
                AddQuad(output, new(left, low), new(right, low), new(left, high), new(right, high), color);
                return;
            }
            var t0 = (minY - a.Y) / (b.Y - a.Y);
            var t1 = (maxY - a.Y) / (b.Y - a.Y);
            var from = Math.Max(0, Math.Min(t0, t1));
            var to = Math.Min(1, Math.Max(t0, t1));
            var first = Vector2.Lerp(a, b, from);
            var last = Vector2.Lerp(a, b, to);
            var dx = new Vector2(halfWidth, 0);
            AddQuad(output, first - dx, first + dx, last - dx, last + dx, color);
        }
        public static void AddVisibleBands(IList<PolygonVertex> output, IList<LineVertex> path, float halfWidth, float minY, float maxY, Vector4 color)
        {
            for (var i = 1; i < path.Count; i++)
            {
                var first = path[i - 1];
                var last = path[i];
                // 透明顶点及无效路径标记都是断段，不得将两侧重新连成判定带。
                if (first.Color.W <= 0 || last.Color.W <= 0 ||
                    first.Dash == new VertexDash(0, 0) || last.Dash == new VertexDash(0, 0))
                    continue;
                AddBand(output, first.Point, last.Point, halfWidth, minY, maxY, color);
            }
        }
        public static void AddOutline(IList<LineVertex> output, float left, float right, float y, Vector4 color, bool relative)
        {
            var dash = relative ? new VertexDash(4, 3) : VertexDash.Solider;
            var a = new Vector2(left, y - 4);
            var b = new Vector2(right, y - 4);
            var c = new Vector2(right, y + 4);
            var d = new Vector2(left, y + 4);
            if (output.Count > 0) output.Add(new(output[output.Count - 1].Point, Vector4.Zero, dash));
            output.Add(new(a, Vector4.Zero, dash));
            output.Add(new(a, color, dash));
            output.Add(new(b, color, dash));
            output.Add(new(c, color, dash));
            output.Add(new(d, color, dash));
            output.Add(new(a, color, dash));
        }
    }
    [Export(typeof(IFumenEditorDrawingTarget))]
    public sealed class BulletHitRangeDrawingTarget : CommonBatchDrawTargetBase<Bullet>
    {
        public override IEnumerable<string> DrawTargetID { get; } = new[] { "BLT" };
        public override DrawingVisible DefaultVisible => DrawingVisible.Design;
        public override int DefaultRenderOrder => 1400;
        public override void Initialize(IRenderManagerImpl impl) { }
        public override void DrawBatch(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, IEnumerable<Bullet> objects)
        {
            if (!target.Editor.ShowBulletHitRanges || !target.Editor.IsDesignMode) return;
            using var polygons = ObjectPool.GetPooledList<PolygonVertex>();
            using var lines = ObjectPool.GetPooledList<LineVertex>();
            var relativeExists = false;
            foreach (var obj in objects)
            {
                if (!target.CheckVisible(obj.TGrid)) continue;
                var x = obj.XGrid.TotalUnit;
                var half = HitRangeGeometry.BulletHalfWidth(obj.TypeValue);
                var left = (float)XGridCalculator.ConvertXGridToX(x - half, target.Editor);
                var right = (float)XGridCalculator.ConvertXGridToX(x + half, target.Editor);
                var y = (float)target.ConvertToViewRelativeY_DefaultSoflanGroup(obj.TGrid);
                var color = HitRangeGeometry.BulletColor(obj.BulletDamageTypeValue);
                var relative = obj.TargetValue == Target.Player;
                relativeExists |= relative;
                if (!relative) HitRangeGeometry.AddQuad(polygons, new(left, y - 4), new(right, y - 4), new(left, y + 4), new(right, y + 4), color);
                var edge = obj.BulletDamageTypeValue == BulletDamageType.Danger ? new Vector4(.8f, .75f, .85f, .7f) : new Vector4(color.X, color.Y, color.Z, .8f);
                HitRangeGeometry.AddOutline(lines, left, right, y, edge, relative);
            }
            if (polygons.Count > 0) builder.DrawPolygon(Primitive.Triangles, polygons);
            if (lines.Count > 0) builder.DrawSimpleLines(lines, 1);
            if (relativeExists)
            {
                var rect = target.CurrentDrawingTargetContext.ViewRelativeRect;
                builder.DrawString(OngekiFumenEditor.Properties.Resources.RelativeBulletHitRangeHint, new((float)rect.MinX + 12, (float)rect.MaxY - 24), Vector2.One, 12, 0, new Vector4(1, .75f, 1, 1), default, default, default);
            }
        }
    }
    [Export(typeof(IFumenEditorDrawingTarget))]
    public sealed class BeamHitRangeDrawingTarget : CommonBatchDrawTargetBase<BeamStart>
    {
        public override IEnumerable<string> DrawTargetID { get; } = new[] { "BMS", "OBS" };
        public override DrawingVisible DefaultVisible => DrawingVisible.Design;
        public override int DefaultRenderOrder => 900;
        public override void Initialize(IRenderManagerImpl impl) { }
        public override void DrawBatch(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, IEnumerable<BeamStart> objects)
        {
            if (!target.Editor.ShowBeamHitRanges || !target.Editor.IsDesignMode) return;
            using var polygons = ObjectPool.GetPooledList<PolygonVertex>();
            using var path = ObjectPool.GetPooledList<LineVertex>();
            var color = new Vector4(1, .85f, 0, .18f);
            var rect = target.CurrentDrawingTargetContext.ViewRelativeRect;
            foreach (var beam in objects)
            {
                if (!target.CheckRangeVisible(beam.MinTGrid, beam.MaxTGrid)) continue;
                path.Clear();
                VisibleLineVerticesQuery.QueryVisibleLineVertices(target, beam, target.Editor.Fumen.SoflansMap.DefaultSoflanList, new VertexDash(0, 0), color, path, preservePathBreaks: true);
                // 原游戏从起点读取 WidthId；横向判定宽度不同于激光贴图宽度。
                var half = (float)Math.Abs(XGridCalculator.ConvertXGridToX(beam.WidthId.WidthJudge / 2d, target.Editor) - XGridCalculator.ConvertXGridToX(0d, target.Editor));
                HitRangeGeometry.AddVisibleBands(polygons, path, half, (float)rect.MinY, (float)rect.MaxY, color);
            }
            if (polygons.Count > 0) builder.DrawPolygon(Primitive.Triangles, polygons);
        }
    }
}
