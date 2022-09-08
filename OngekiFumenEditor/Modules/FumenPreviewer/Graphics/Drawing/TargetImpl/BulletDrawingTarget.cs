using Caliburn.Micro;
using FontStashSharp;
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
using static OngekiFumenEditor.Base.OngekiObjects.Bullet;
using static OngekiFumenEditor.Base.OngekiObjects.BulletPallete;
using Vector = OngekiFumenEditor.Modules.FumenPreviewer.Graphics.PrimitiveValue.Vector;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl
{
    [Export(typeof(IDrawingTarget))]
    public class BulletDrawingTarget : CommonDrawTargetBase<Bullet>, IDisposable
    {
        Dictionary<BulletDamageType, Dictionary<BulletType, Texture>> spritesMap = new();
        Dictionary<Texture, Vector2> spritesSize = new();
        Dictionary<Texture, Vector> spritesOriginOffset = new();

        private IStringDrawing stringDrawing;
        private ITextureDrawing textureDrawing;

        public override IEnumerable<string> DrawTargetID { get; } = new[] { Bullet.CommandName };

        public BulletDrawingTarget() : base()
        {
            Texture LoadTex(string rPath)
            {
                var info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\" + rPath, UriKind.Relative));
                using var bitmap = Image.FromStream(info.Stream) as Bitmap;
                return new Texture(bitmap);
            }

            void SetTexture(BulletDamageType k1, BulletType k2, string rPath, Vector2 size, Vector origOffset)
            {
                if (!spritesMap.TryGetValue(k1, out var dic))
                {
                    dic = new Dictionary<BulletType, Texture>();
                    spritesMap[k1] = dic;
                }

                var tex = LoadTex(rPath);
                dic[k2] = tex;

                spritesSize[tex] = size;
                spritesOriginOffset[tex] = origOffset;
            }

            var size = new Vector2(40, 40);
            var origOffset = new Vector(0, 0);
            SetTexture(BulletDamageType.Normal, BulletType.Circle, "nt_mine_red.png", size, origOffset);
            SetTexture(BulletDamageType.Hard, BulletType.Circle, "nt_mine_pur.png", size, origOffset);
            SetTexture(BulletDamageType.Danger, BulletType.Circle, "nt_mine_blk.png", size, origOffset);

            size = new(30, 80);
            origOffset = new Vector(0, 35);
            SetTexture(BulletDamageType.Normal, BulletType.Needle, "tri_bullet0.png", size, origOffset);
            SetTexture(BulletDamageType.Hard, BulletType.Needle, "tri_bullet1.png", size, origOffset);
            SetTexture(BulletDamageType.Danger, BulletType.Needle, "tri_bullet2.png", size, origOffset);

            size = new(30, 80);
            origOffset = new Vector(0, 35);
            SetTexture(BulletDamageType.Normal, BulletType.Square, "sqrt_bullet0.png", size, origOffset);
            SetTexture(BulletDamageType.Hard, BulletType.Square, "sqrt_bullet1.png", size, origOffset);
            SetTexture(BulletDamageType.Danger, BulletType.Square, "sqrt_bullet2.png", size, origOffset);

            stringDrawing = IoC.Get<IStringDrawing>();
            textureDrawing = IoC.Get<ITextureDrawing>();
        }

        public float CalculateBulletMsecTime(IFumenPreviewer target, Bullet obj, float userSpeed = 2.35f)
        {
            //const float fat = 3.95f;
            //var time =  32.5f * fat / (Math.Max(4.7f, 0.2f * userSpeed) * (/*obj.ReferenceBulletPallete?.Speed ??*/ 1f)) * 16.666666f;
            var time = (float)target.ViewHeight / (obj.ReferenceBulletPallete?.Speed ?? 1f);
            return time;
        }

        public override void Draw(IFumenPreviewer target, Bullet obj)
        {
            var appearOffsetTime = CalculateBulletMsecTime(target, obj);

            /*
            --------------------------- toTime 
                    \
                     \
                      \
                       \
                        \
                         O      <- currentTime
                          bell
                           \
                            \
                             \
                              \
                               \
            ---------------------------- fromTime = toTime - appearOffsetTime
             */

            var fromX = XGridCalculator.ConvertXGridToX(obj.ReferenceBulletPallete?.CalculateFromXGrid(obj.XGrid, target.Fumen) ?? obj.XGrid, 30, target.ViewWidth, 1);
            var toX = XGridCalculator.ConvertXGridToX(obj.ReferenceBulletPallete?.CalculateToXGrid(obj.XGrid, target.Fumen) ?? obj.XGrid, 30, target.ViewWidth, 1);

            //计算向量化的物件运动时间
            var toTime = TGridCalculator.ConvertTGridToY(obj.TGrid, target.Fumen.BpmList, 1, 240);
            var fromTime = toTime - appearOffsetTime;
            var currentTime = MathUtils.Limit(target.CurrentPlayTime, toTime, fromTime);
            var precent = (currentTime - fromTime) / appearOffsetTime;
            //Log.LogDebug($"precent : {precent * 100:F2}");
            if (target.CurrentPlayTime < fromTime)
                return;

            var timeX = MathUtils.CalculateXFromTwoPointFormFormula(currentTime, fromX, fromTime, toX, toTime);

            timeX = MathUtils.Limit(timeX, fromX, toX);
            var timeY = target.CurrentPlayTime + target.ViewHeight * (1 - precent);

            var pos = new Vector((float)timeX, (float)timeY);

            var texture = spritesMap[obj.BulletDamageTypeValue][obj.ReferenceBulletPallete.TypeValue];
            var size = spritesSize[texture];
            var origOffset = spritesOriginOffset[texture];

            var rotate = Math.Atan((toX - fromX) / (toTime - fromTime));
            var offsetPos = pos + origOffset;
            textureDrawing.Draw(target, texture, new (Vector2, Vector2, float)[] { (new(size.X, size.Y), new(offsetPos.X, offsetPos.Y), (float)rotate) });
            DrawPallateStr(target, obj, pos + origOffset);
        }

        private void DrawPallateStr(IFumenPreviewer target, IBulletPalleteReferencable obj, Vector pos)
        {
            if (obj.ReferenceBulletPallete is null)
                return;
            stringDrawing.Draw($"{obj.ReferenceBulletPallete.StrID}", new(pos.X - target.ViewWidth / 2, pos.Y + 5), System.Numerics.Vector2.One, 16, 0, System.Numerics.Vector4.One, new(0.5f, 0.5f), default, target, default, out _);
        }

        public void Dispose()
        {
            spritesMap.SelectMany(x => x.Value.Values).ForEach(x => x.Dispose());
            spritesMap.Clear();
        }
    }
}
