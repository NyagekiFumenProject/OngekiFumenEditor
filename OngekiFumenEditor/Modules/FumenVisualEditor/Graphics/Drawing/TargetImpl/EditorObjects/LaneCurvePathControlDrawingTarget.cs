using Caliburn.Micro;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Numerics;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    public class LaneCurvePathControlDrawingTarget : CommonBatchDrawTargetBase<LaneCurvePathControlObject>, IDisposable
    {
        private IImage texture;
        private ITextureDrawing textureDrawing;
        private IHighlightBatchTextureDrawing highlightDrawing;
        private IStringDrawing stringDrawing;
        private ILineDrawing lineDrawing;
        private Vector2 size;
        private static readonly Vector4 Transparent = new Vector4(0, 0, 0, 0);
        private static readonly VertexDash LineDash = new (6, 3);

        public override IEnumerable<string> DrawTargetID { get; } = new[] { LaneCurvePathControlObject.CommandName };
        public override DrawingVisible DefaultVisible => DrawingVisible.Design;

        public override int DefaultRenderOrder => 2000;

        public LaneCurvePathControlDrawingTarget()
        {
            texture = ResourceUtils.OpenReadTextureFromFile(@".\Resources\editor\commonCircle.png");
            if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile("commonCircle", out size, out _))
                size = new Vector2(16, 16);

            textureDrawing = IoC.Get<IRenderManager>().TextureDrawing;
            stringDrawing = IoC.Get<IRenderManager>().StringDrawing;
            highlightDrawing = IoC.Get<IRenderManager>().HighlightBatchTextureDrawing;
            lineDrawing = IoC.Get<IRenderManager>().SimpleLineDrawing;
        }

        public void Dispose()
        {
            texture = null;
            texture.Dispose();
        }

        public override void DrawBatch(IFumenEditorDrawingContext target, IEnumerable<LaneCurvePathControlObject> objs)
        {
            var isAlwaysShow = target.Editor.IsShowCurveControlAlways;
            using var d = objs.Where(x => x.RefCurveObject.IsSelected || x.RefCurveObject.IsAnyControlSelecting || isAlwaysShow).Select(x => (
                (float)target.ConvertToY_DefaultSoflanGroup(x.TGrid),
                (float)XGridCalculator.ConvertXGridToX(x.XGrid, target.Editor),
                x
            )).ToListWithObjectPool<(float y, float x, LaneCurvePathControlObject obj)>(out var list);

            if (list.Count == 0)
                return;

            var lineVertices = list.GroupBy(x => x.obj.RefCurveObject).SelectMany(item =>
            {
                IEnumerable<LineVertex> gen()
                {
                    var refConnectableObject = item.Key;

                    var hash = refConnectableObject.ReferenceStartObject.GetHashCode();
                    var alpha = (byte)((hash >> 24) & 0xFF);
                    var color = new Vector4((((hash >> 16) & 0xFF) ^ alpha) / 255f / 2 + 0.5f, (((hash >> 8) & 0xFF) ^ alpha) / 255f / 2 + 0.5f, ((hash & 0xFF) ^ alpha) / 255f / 2 + 0.5f, 1f);
                    //var color = new Vector4(1, 1, 1, 1f);

                    var ry = (float)target.ConvertToY_DefaultSoflanGroup(refConnectableObject.TGrid);
                    var rx = (float)XGridCalculator.ConvertXGridToX(refConnectableObject.XGrid, target.Editor);
                    yield return new LineVertex(new(rx, ry), Transparent, LineDash);
                    yield return new LineVertex(new(rx, ry), color, LineDash);
                    foreach (var curve in item.OrderBy(x => x.obj.Index).Reverse())
                        yield return new LineVertex(new(curve.x, curve.y), color, LineDash);
                    var parentConnectableObject = refConnectableObject.PrevObject;
                    var rpy = (float)target.ConvertToY_DefaultSoflanGroup(parentConnectableObject.TGrid);
                    var rpx = (float)XGridCalculator.ConvertXGridToX(parentConnectableObject.XGrid, target.Editor);
                    yield return new LineVertex(new(rpx, rpy), color, LineDash);
                    yield return new LineVertex(new(rpx, rpy), Transparent, LineDash);
                }
                return gen();
            });
            lineDrawing.Draw(target, lineVertices, 2);

            highlightDrawing.Draw(target, texture, list.Where(x => x.obj.IsSelected).Select(x => (size * 1.25f, new Vector2(x.x, x.y), 0f, Vector4.One)));
            textureDrawing.Draw(target, texture, list.Select(x => (size, new Vector2(x.x, x.y), 0f, Vector4.One)));
            foreach ((var y, var x, var obj) in list)
                target.RegisterSelectableObject(obj, new Vector2(x, y), size);

            foreach (var item in list)
                stringDrawing.Draw(item.obj.Index.ToString(), new(item.x, item.y + 4), Vector2.One, 15, 0, new(1, 0, 1, 1), new(0.5f, 0.5f),
                     IStringDrawing.StringStyle.Bold, target, default, out _);
        }
    }
}
