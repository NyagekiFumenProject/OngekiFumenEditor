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
using static OngekiFumenEditor.Base.OngekiObjects.BulletPallete;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl.OngekiObjects
{
    [Export(typeof(IDrawingTarget))]
    [Export(typeof(TapDrawingTarget))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class TapDrawingTarget : CommonBatchDrawTargetBase<Tap>, IDisposable
    {
        public override IEnumerable<string> DrawTargetID { get; } = new[] { "TAP", "CTP", "XTP" };

        private Texture redTexture;
        private Texture greenTexture;
        private Texture blueTexture;
        private Texture wallTexture;
        private Texture tapExTexture;

        private Texture prevTexture = default;

        private Vector2 tapSize = new Vector2(40, 16);
        private Vector2 exTapEffSize = new Vector2(40, 16);
        private Vector2 leftWallSize = new Vector2(40, 40);
        private Vector2 rightWallSize = new Vector2(-40, 40);

        private List<(Vector2 size, Vector2 pos, float rotate)> exTapList = new();

        private IBatchTextureDrawing batchTextureDrawing;
        private IFumenPreviewer target;

        public TapDrawingTarget() : base()
        {
            var info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\mu3_nt_tap_02.png", UriKind.Relative));
            using var bitmap = Image.FromStream(info.Stream) as Bitmap;
            redTexture = new Texture(bitmap, "mu3_nt_tap_02.png");

            info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\mu3_nt_extap_02.png", UriKind.Relative));
            using var bitmap1 = Image.FromStream(info.Stream) as Bitmap;
            greenTexture = new Texture(bitmap1, "mu3_nt_extap_02.png");


            info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\mu3_nt_hold_02.png", UriKind.Relative));
            using var bitmap2 = Image.FromStream(info.Stream) as Bitmap;
            blueTexture = new Texture(bitmap2, "mu3_nt_hold_02.png");

            info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\walltap.png", UriKind.Relative));
            using var bitmap3 = Image.FromStream(info.Stream) as Bitmap;
            wallTexture = new Texture(bitmap3, "walltap.png");

            info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\tap_exEff.png", UriKind.Relative));
            using var bitmap4 = Image.FromStream(info.Stream) as Bitmap;
            tapExTexture = new Texture(bitmap4, "tap_exEff.png");

            batchTextureDrawing = IoC.Get<IBatchTextureDrawing>();
        }

        void SyncTapTexture(IFumenPreviewer target, Texture texture)
        {
            if (prevTexture == texture)
                return;
            if (prevTexture is not null)
                batchTextureDrawing.End();
            batchTextureDrawing.Begin(target, texture);
            prevTexture = texture;
        }

        public void Draw(IFumenPreviewer target, LaneType? laneType, TGrid tGrid, XGrid xGrid, bool isExTap)
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

            SyncTapTexture(target, texture);
            batchTextureDrawing.PostSprite(size, pos, 0f);
            if (isExTap)
                exTapList.Add((new(68, 30), pos, 0f));
        }

        public void Dispose()
        {
            redTexture?.Dispose();
            greenTexture?.Dispose();
            blueTexture?.Dispose();
            wallTexture?.Dispose();
        }

        public override void Begin(IFumenPreviewer target)
        {
            base.Begin(target);
            this.target = target;
            prevTexture = default;
        }

        public override void DrawBatch(IFumenPreviewer target, IEnumerable<Tap> objs)
        {
            foreach (var tap in objs)
            {
                var type = tap.ReferenceLaneStart?.LaneType;
                Draw(target, type, tap.TGrid, tap.XGrid, tap.IsCritical);
            }
        }

        public override void End()
        {
            base.End();
            batchTextureDrawing.End();
            //draw extap
            batchTextureDrawing.Draw(target, tapExTexture, exTapList);
            exTapList.Clear();
            prevTexture = default;
        }
    }
}
