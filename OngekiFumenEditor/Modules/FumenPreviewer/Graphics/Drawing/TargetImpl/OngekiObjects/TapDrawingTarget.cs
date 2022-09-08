using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.PrimitiveValue;
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
    [Export(typeof(TapDrawingTarget))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class TapDrawingTarget : CommonDrawTargetBase<Tap>, IDisposable
    {
        public override IEnumerable<string> DrawTargetID { get; } = new[] { "TAP", "CTP", "XTP" };

        private Texture redTexture;
        private Texture greenTexture;
        private Texture blueTexture;
        private Texture wallTexture;

        private Vector2 tapSize = new Vector2(40, 16);
        private Vector2 leftWallSize = new Vector2(40, 40);
        private Vector2 rightWallSize = new Vector2(-40, 40);

        private ITextureDrawing textureDrawing;

        public TapDrawingTarget() : base()
        {
            var info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\mu3_nt_tap_02.png", UriKind.Relative));
            using var bitmap = Image.FromStream(info.Stream) as Bitmap;
            redTexture = new Texture(bitmap);

            info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\mu3_nt_extap_02.png", UriKind.Relative));
            using var bitmap1 = Image.FromStream(info.Stream) as Bitmap;
            greenTexture = new Texture(bitmap1);


            info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\mu3_nt_hold_02.png", UriKind.Relative));
            using var bitmap2 = Image.FromStream(info.Stream) as Bitmap;
            blueTexture = new Texture(bitmap2);

            info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\walltap.png", UriKind.Relative));
            using var bitmap3 = Image.FromStream(info.Stream) as Bitmap;
            wallTexture = new Texture(bitmap3);

            textureDrawing = IoC.Get<ITextureDrawing>();
        }

        public override void Draw(IFumenPreviewer target, Tap ongekiObject)
        {
            var type = ongekiObject.ReferenceLaneStart?.LaneType;
            Draw(target, type, ongekiObject.TGrid, ongekiObject.XGrid);
        }

        public void Draw(IFumenPreviewer target, LaneType? laneType, TGrid tGrid, XGrid xGrid)
        {
            var texture = laneType switch
            {
                LaneType.Left => redTexture,
                LaneType.Center => greenTexture,
                LaneType.Right => blueTexture,
                LaneType.WallRight or LaneType.WallLeft => wallTexture,
                _ => default
            };

            if (texture is null)
                return;

            var size = laneType switch
            {
                LaneType.WallRight => rightWallSize,
                LaneType.WallLeft => leftWallSize,
                _ => tapSize
            };

            var x = XGridCalculator.ConvertXGridToX(xGrid, 30, target.ViewWidth, 1);
            var y = TGridCalculator.ConvertTGridToY(tGrid, target.Fumen.BpmList, 1.0, 240);

            var pos = new Vector2((float)x, (float)y);

            textureDrawing.Draw(target, texture, new[] { (size, pos, 0f) });
        }

        public void Dispose()
        {
            redTexture?.Dispose();
            greenTexture?.Dispose();
            blueTexture?.Dispose();
            wallTexture?.Dispose();
        }
    }
}
