using Caliburn.Micro;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Text;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Numerics;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    public sealed class LaneCurvePathControlDrawingTarget : CommonBatchDrawTargetBase<LaneCurvePathControlObject>, IDisposable
    {
        private IImage texture;
        private Vector2 size;
        private static readonly Vector4 Transparent = new Vector4(0, 0, 0, 0);
        private static readonly VertexDash LineDash = new(6, 3);

        public override IEnumerable<string> DrawTargetID { get; } = new[] { LaneCurvePathControlObject.CommandName };
        public override DrawingVisible DefaultVisible => DrawingVisible.Design;

        public override int DefaultRenderOrder => 2000;

        private readonly struct CtrlPoint(float y, float x, LaneCurvePathControlObject obj)
        {
            public readonly float Y = y;
            public readonly float X = x;
            public readonly LaneCurvePathControlObject Obj = obj;
        }

        private static readonly IComparer<CtrlPoint> indexDescCmp =
            Comparer<CtrlPoint>.Create(static (a, b) => b.Obj.Index.CompareTo(a.Obj.Index));

        private static readonly Dictionary<int, string> indexStringCache = new();

        public override void Initialize(IRenderManagerImpl impl)
        {
            texture = ResourceUtils.OpenReadTextureFromFile(impl, @".\Resources\editor\commonCircle.png");
            if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile("commonCircle", out size, out _))
                size = new Vector2(16, 16);
        }

        public void Dispose()
        {
            texture = null;
            texture.Dispose();
        }

        public override void DrawBatch(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, IEnumerable<LaneCurvePathControlObject> objs)
        {
            var isAlwaysShow = target.Editor.IsShowCurveControlAlways;

            using var filtered = ObjectPool.GetPooledList<CtrlPoint>();
            using var allTex = ObjectPool.GetPooledList<TextureInstance>();
            using var selectedTex = ObjectPool.GetPooledList<TextureInstance>();
            using var buckets = ObjectPool.GetPooledDictionary<ConnectableChildObjectBase, IPooledList<CtrlPoint>>();

            // 一次扫描完成: 过滤 + 纹理实例收集 + 按 RefCurve 分桶
            foreach (var obj in objs)
            {
                var refCurve = obj.RefCurveObject;
                if (!(refCurve.IsSelected || refCurve.IsAnyControlSelecting || isAlwaysShow))
                    continue;

                var y = (float)target.ConvertToViewRelativeY_DefaultSoflanGroup(obj.TGrid);
                var x = (float)XGridCalculator.ConvertXGridToX(obj.XGrid, target.Editor);
                var point = new CtrlPoint(y, x, obj);

                filtered.Add(point);
                allTex.Add(new TextureInstance(size, new Vector2(x, y), 0f, Vector4.One));
                if (obj.IsSelected)
                    selectedTex.Add(new TextureInstance(size * 1.25f, new Vector2(x, y), 0f, Vector4.One));

                if (!buckets.TryGetValue(refCurve, out var bucket))
                {
                    bucket = ObjectPool.GetPooledList<CtrlPoint>();
                    buckets[refCurve] = bucket;
                }
                bucket.Add(point);
            }

            if (filtered.Count == 0)
                return;

            using var lineVertices = ObjectPool.GetPooledList<LineVertex>();

            foreach (var kv in buckets)
            {
                var refConnectableObject = kv.Key;
                var items = kv.Value;
                try
                {
                    // 原版: item.OrderBy(Index).Reverse() -> 改为单次原地 Sort 降序
                    items.Sort(indexDescCmp);

                    var hash = refConnectableObject.ReferenceStartObject.GetHashCode();
                    var alpha = (byte)((hash >> 24) & 0xFF);
                    var color = new Vector4(
                        (((hash >> 16) & 0xFF) ^ alpha) / 255f / 2 + 0.5f,
                        (((hash >> 8) & 0xFF) ^ alpha) / 255f / 2 + 0.5f,
                        ((hash & 0xFF) ^ alpha) / 255f / 2 + 0.5f,
                        1f);

                    var ry = (float)target.ConvertToViewRelativeY_DefaultSoflanGroup(refConnectableObject.TGrid);
                    var rx = (float)XGridCalculator.ConvertXGridToX(refConnectableObject.XGrid, target.Editor);
                    lineVertices.Add(new LineVertex(new(rx, ry), Transparent, LineDash));
                    lineVertices.Add(new LineVertex(new(rx, ry), color, LineDash));
                    for (var i = 0; i < items.Count; i++)
                        lineVertices.Add(new LineVertex(new(items[i].X, items[i].Y), color, LineDash));

                    var parentConnectableObject = refConnectableObject.PrevObject;
                    var rpy = (float)target.ConvertToViewRelativeY_DefaultSoflanGroup(parentConnectableObject.TGrid);
                    var rpx = (float)XGridCalculator.ConvertXGridToX(parentConnectableObject.XGrid, target.Editor);
                    lineVertices.Add(new LineVertex(new(rpx, rpy), color, LineDash));
                    lineVertices.Add(new LineVertex(new(rpx, rpy), Transparent, LineDash));
                }
                finally
                {
                    items.Dispose();
                }
            }

            builder.DrawSimpleLines(lineVertices, 2);
            builder.DrawHighlightBatchTexture(texture, selectedTex);
            builder.DrawTexture(texture, allTex);

            for (var i = 0; i < filtered.Count; i++)
            {
                var p = filtered[i];
                target.RegisterSelectableObject(p.Obj, new Vector2(p.X, p.Y), size);
                builder.DrawString(GetIndexString(p.Obj.Index), new(p.X, p.Y + 4), Vector2.One, 15, 0,
                    new(1, 0, 1, 1), new(0.5f, 0.5f), FontStyle.Bold, default);
            }
        }

        private static string GetIndexString(int idx)
        {
            if (!indexStringCache.TryGetValue(idx, out var s))
                indexStringCache[idx] = s = idx.ToString();
            return s;
        }
    }
}
