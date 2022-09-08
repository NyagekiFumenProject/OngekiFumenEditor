using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl.OngekiObjects
{
    [Export(typeof(IDrawingTarget))]
    public class FlickDrawingTarget : CommonDrawTargetBase<Flick>, IDisposable
    {
        private Texture texture;
        private Vector2 leftSize;
        private Vector2 rightSize;

        private ITextureDrawing textureDrawing;

        public override IEnumerable<string> DrawTargetID { get; } = new[] { "FLK", "CFK" };

        public FlickDrawingTarget() : base()
        {
            var info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\flick.png", UriKind.Relative));
            using var bitmap = Image.FromStream(info.Stream) as Bitmap;
            texture = new Texture(bitmap);
            leftSize = new Vector2(104, 69.333f);
            rightSize = new Vector2(-104, 69.333f);

            textureDrawing = IoC.Get<ITextureDrawing>();
        }

        public override void Draw(IFumenPreviewer target, Flick obj)
        {
            var x = XGridCalculator.ConvertXGridToX(obj.XGrid, 30, target.ViewWidth, 1);
            var y = TGridCalculator.ConvertTGridToY(obj.TGrid, target.Fumen.BpmList, 1.0, 240) + 24;
            var pos = new Vector2((float)x, (float)y);
            var size = obj.Direction == Flick.FlickDirection.Right ? rightSize : leftSize;

            textureDrawing.Draw(target, texture, new[] { (size, pos, 0f) });
        }

        public void Dispose()
        {
            texture?.Dispose();
            texture = null;
        }
    }
}
