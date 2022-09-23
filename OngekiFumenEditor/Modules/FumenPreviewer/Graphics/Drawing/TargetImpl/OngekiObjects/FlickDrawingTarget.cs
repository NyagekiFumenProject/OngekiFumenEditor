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
    public class FlickDrawingTarget : CommonBatchDrawTargetBase<Flick>, IDisposable
    {
        private Texture texture;
        private Texture exFlickEffTexture;

        private Vector2 leftSize;
        private Vector2 rightSize;
        private Vector2 exTapEffSize;

        private List<(Vector2, Vector2, float)> exFlickList = new();

        private IBatchTextureDrawing batchTextureDrawing;

        public override IEnumerable<string> DrawTargetID { get; } = new[] { "FLK", "CFK" };

        public FlickDrawingTarget() : base()
        {
            texture = ResourceUtils.OpenReadTextureFromResource(@"Modules\FumenVisualEditor\Views\OngekiObjects\flick.png");
            exFlickEffTexture = ResourceUtils.OpenReadTextureFromResource(@"Modules\FumenVisualEditor\Views\OngekiObjects\exflick_Eff.png");

            leftSize = new Vector2(104, 69.333f);
            rightSize = new Vector2(-104, 69.333f);
            exTapEffSize = new Vector2(106, 67f);

            batchTextureDrawing = IoC.Get<IBatchTextureDrawing>();
        }

        public override void DrawBatch(IFumenPreviewer target, IEnumerable<Flick> objs)
        {
            batchTextureDrawing.Begin(target, texture);
            foreach (var obj in objs)
            {
                var x = XGridCalculator.ConvertXGridToX(obj.XGrid, 30, target.ViewWidth, 1);
                var y = TGridCalculator.ConvertTGridToY(obj.TGrid, target.Fumen.BpmList, 1.0, 240) + 24;
                var pos = new Vector2((float)x, (float)y);
                var size = obj.Direction == Flick.FlickDirection.Right ? rightSize : leftSize;
                batchTextureDrawing.PostSprite(size, pos, 0f);

                if (obj.IsCritical)
                {
                    var exTapSize = exTapEffSize;
                    exTapSize.X = Math.Sign(size.X) * exTapSize.X;
                    pos.Y -= 1;

                    exFlickList.Add((exTapSize, pos, 0));
                }
            }
            batchTextureDrawing.End();

            batchTextureDrawing.Draw(target, exFlickEffTexture, exFlickList);

            exFlickList.Clear();
        }

        public void Dispose()
        {
            texture?.Dispose();
            texture = null;
        }
    }
}
