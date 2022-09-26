using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.DefaultDrawingImpl.StringDrawing.String;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
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

        private Vector2 tapSize = new Vector2(40, 16);
        private Vector2 exTapEffSize = new Vector2(40, 16);
        private Vector2 leftWallSize = new Vector2(40, 40);
        private Vector2 rightWallSize = new Vector2(-40, 40);

        private Dictionary<Texture, List<(Vector2 size, Vector2 pos, float rotate)>> normalList = new();
        private Dictionary<Texture, List<(Vector2 size, Vector2 pos, float rotate)>> exList = new();
        private Dictionary<Texture, List<(Vector2 size, Vector2 pos, float rotate)>> selectTapList = new();

        private Texture wallExTexture;
        private IBatchTextureDrawing batchTextureDrawing;
        private IHighlightBatchTextureDrawing highlightDrawing;

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

            void init(Texture texture)
            {
                normalList[texture] = new();
                selectTapList[texture] = new();
            }

            init(redTexture);
            init(greenTexture);
            init(blueTexture);
            init(wallTexture);

            exList[tapExTexture] = new();
            exList[wallExTexture] = new();

            batchTextureDrawing = IoC.Get<IBatchTextureDrawing>();
            highlightDrawing = IoC.Get<IHighlightBatchTextureDrawing>();
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

            normalList[texture].Add((size, pos, 0f));
            if (tap.IsSelected)
            {
                if (laneType == LaneType.WallLeft || laneType == LaneType.WallRight)
                {
                    size = new(Math.Sign(size.X) * 42, 42);
                }
                else
                {
                    size = tapSize * new Vector2(1.5f,1.5f);
                }

                selectTapList[texture].Add((size, pos, 0f));
            }
            if (isCritical)
            {
                if (laneType == LaneType.WallLeft || laneType == LaneType.WallRight)
                {
                    size = new(Math.Sign(size.X) * 39, 39);
                    texture = wallExTexture;
                }
                else
                {
                    size = new(68, 30);
                    texture = tapExTexture;
                }

                exList[texture].Add((size, pos, 0f));
            }

            target.RegisterSelectableObject(tap, pos, size);
        }

        private void ClearList()
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void clear(Dictionary<Texture, List<(Vector2 size, Vector2 pos, float rotate)>> map)
            {
                foreach (var list in map.Values)
                    list.Clear();
            }

            clear(normalList);
            clear(exList);
            clear(selectTapList);
        }

        public void Dispose()
        {
            redTexture?.Dispose();
            greenTexture?.Dispose();
            blueTexture?.Dispose();
            wallTexture?.Dispose();
        }


        public override void DrawBatch(IFumenPreviewer target, IEnumerable<Tap> objs)
        {
            foreach (var tap in objs)
                Draw(target, tap.ReferenceLaneStart?.LaneType, tap, tap.IsCritical);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void draw(Dictionary<Texture, List<(Vector2 size, Vector2 pos, float rotate)>> map)
            {
                foreach (var item in map)
                    batchTextureDrawing.Draw(target, item.Key, item.Value);
            }

            foreach (var item in selectTapList)
                highlightDrawing.Draw(target, item.Key, item.Value);
            draw(exList);
            draw(normalList);

            ClearList();
        }
    }
}
