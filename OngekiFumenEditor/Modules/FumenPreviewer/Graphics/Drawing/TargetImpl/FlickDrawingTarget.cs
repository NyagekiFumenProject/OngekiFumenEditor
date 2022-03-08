using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.PrimitiveValue;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Utils;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl
{
    [Export(typeof(IDrawingTarget))]
    public class FlickDrawingTarget : CommonSpriteDrawTargetBase<Flick>
    {
        private Texture texture;
        private Vector2 leftSize;
        private Vector2 rightSize;

        public override IEnumerable<string> DrawTargetID { get; } = new[] { "FLK", "CFK" };

        public FlickDrawingTarget() : base()
        {
            var info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\flick.png", UriKind.Relative));
            using var bitmap = Image.FromStream(info.Stream) as Bitmap;
            texture = new Texture(bitmap);
            leftSize = new Vector2(104, 69.333f);
            rightSize = new Vector2(-104, 69.333f);
        }

        public override void Draw(Flick obj, OngekiFumen fumen)
        {
            var x = XGridCalculator.ConvertXGridToX(obj.XGrid, 30, Previewer.ViewWidth, 1);
            var y = TGridCalculator.ConvertTGridToY(obj.TGrid, fumen.BpmList, 240);
            var pos = new Vector((float)x, (float)y);
            var size = obj.Direction == Flick.FlickDirection.Right ? rightSize : leftSize;

            Draw(texture, size, pos, 0);
        }

        public override void Dispose()
        {
            base.Dispose();
            texture?.Dispose();
            texture = null;
        }
    }
}
