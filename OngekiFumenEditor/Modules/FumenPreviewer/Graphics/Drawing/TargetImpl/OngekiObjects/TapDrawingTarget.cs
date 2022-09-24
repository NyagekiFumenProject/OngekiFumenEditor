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
        private List<(Vector2 size, Vector2 pos, float rotate)> exWallTapList = new();
        private Texture wallExTexture;
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

            info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\walltap_Eff.png", UriKind.Relative));
            using var bitmap5 = Image.FromStream(info.Stream) as Bitmap;
            wallExTexture = new Texture(bitmap5, "walltap_Eff.png");

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

        public void Draw(IFumenPreviewer target, LaneType? laneType, OngekiMovableObjectBase tap, bool isCritical)
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

            var x = XGridCalculator.ConvertXGridToX(tap.XGrid, 30, target.ViewWidth, 1);
            var y = TGridCalculator.ConvertTGridToY(tap.TGrid, target.Fumen.BpmList, 1.0, 240);

            var pos = new Vector2((float)x, (float)y);

            SyncTapTexture(target, texture);
            batchTextureDrawing.PostSprite(size, pos, 0f);
            if (isCritical)
            {
                if (laneType == LaneType.WallLeft || laneType == LaneType.WallRight)
                {
                    size.Y = 39;
                    size.X = Math.Sign(size.X) * 39;
                    exWallTapList.Add((size, pos, 0f));
                }
                else
                {
                    exTapList.Add((new(68, 30), pos, 0f));
                }
            }

            target.RegisterSelectableObject(tap, pos, size);
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
        }

        public override void DrawBatch(IFumenPreviewer target, IEnumerable<Tap> objs)
        {
            foreach (var tap in objs)
                Draw(target, tap.ReferenceLaneStart?.LaneType, tap, tap.IsCritical);
        }

        public override void End()
        {
            base.End();
            batchTextureDrawing.End();

            batchTextureDrawing.Draw(target, tapExTexture, exTapList);
            batchTextureDrawing.Draw(target, wallExTexture, exWallTapList);

            exTapList.Clear();
            exWallTapList.Clear();

            prevTexture = default;
        }
    }
}
