using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Base;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    public class LaneCurvePathControlDrawingTarget : CommonBatchDrawTargetBase<LaneCurvePathControlObject>, IDisposable
    {
        private Texture texture;
        private ITextureDrawing textureDrawing;
        private IHighlightBatchTextureDrawing highlightDrawing;
        private IStringDrawing stringDrawing;

        public override IEnumerable<string> DrawTargetID { get; } = new[] { LaneCurvePathControlObject.CommandName };

        public override int DefaultRenderOrder => 2000;

        public LaneCurvePathControlDrawingTarget()
        {
            texture = ResourceUtils.OpenReadTextureFromResource(@"Modules\FumenVisualEditor\Views\OngekiObjects\CC.png");
            textureDrawing = IoC.Get<IBatchTextureDrawing>();
            stringDrawing = IoC.Get<IStringDrawing>();
            highlightDrawing = IoC.Get<IHighlightBatchTextureDrawing>();
        }

        public void Dispose()
        {
            texture = null;
            texture.Dispose();
        }

        public override void DrawBatch(IFumenEditorDrawingContext target, IEnumerable<LaneCurvePathControlObject> objs)
        {
            var size = new Vector2(16, 16);

            using var d = objs.Where(x => x.RefCurveObject.IsSelected || x.RefCurveObject.IsAnyControlSelecting).Select(x => (
                (float)TGridCalculator.ConvertTGridToY(x.TGrid, target.Editor),
                (float)XGridCalculator.ConvertXGridToX(x.XGrid, target.Editor),
                x
            )).ToHashSetWithObjectPool<(float y, float x, LaneCurvePathControlObject obj)>(out var list);

            highlightDrawing.Draw(target, texture, list.Where(x => x.obj.IsSelected).Select(x => (size * 1.25f, new Vector2(x.x, x.y), 0f)));
            textureDrawing.Draw(target, texture, list.Select(x => (size, new Vector2(x.x, x.y), 0f)));
            foreach ((var y, var x, var obj) in list)
                target.RegisterSelectableObject(obj, new Vector2(x, y), size);

            foreach (var item in list)
                stringDrawing.Draw(item.obj.Index.ToString(), new(item.x, item.y + 4), Vector2.One, 15, 0, new(1, 0, 1, 1), new(0.5f, 0.5f),
                     IStringDrawing.StringStyle.Bold, target, default, out _);
        }
    }
}
