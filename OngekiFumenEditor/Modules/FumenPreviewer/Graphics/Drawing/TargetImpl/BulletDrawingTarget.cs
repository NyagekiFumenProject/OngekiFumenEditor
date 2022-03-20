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
using static OngekiFumenEditor.Base.OngekiObjects.Bullet;
using static OngekiFumenEditor.Base.OngekiObjects.BulletPallete;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl
{
    [Export(typeof(IDrawingTarget))]
    public class BulletDrawingTarget : CommonSpriteDrawTargetBase<Bullet>
    {
        Dictionary<string, Dictionary<string, Texture>> spritesMap = new();
        Dictionary<Texture, Vector2> spritesSize = new();
        Dictionary<Texture, Vector> spritesOriginOffset = new();

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
                    dic = new Dictionary<string, Texture>();
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
        }

        public float CalculateBulletMsecTime(Bullet obj, float userSpeed = 2.35f)
        {
            //const float fat = 3.95f;
            //var time =  32.5f * fat / (Math.Max(4.7f, 0.2f * userSpeed) * (/*obj.ReferenceBulletPallete?.Speed ??*/ 1f)) * 16.666666f;
            var time = (float)Previewer.ViewHeight / (obj.ReferenceBulletPallete?.Speed ?? 1f);
            return time;
        }

        public override void Draw(Bullet obj, OngekiFumen fumen)
        {
            var appearOffsetTime = CalculateBulletMsecTime(obj);

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

            var fromX = XGridCalculator.ConvertXGridToX(obj.ReferenceBulletPallete?.CalculateFromXGrid(obj.XGrid, fumen) ?? obj.XGrid, 30, Previewer.ViewWidth, 1);
            var toX = XGridCalculator.ConvertXGridToX(obj.ReferenceBulletPallete?.CalculateToXGrid(obj.XGrid, fumen) ?? obj.XGrid, 30, Previewer.ViewWidth, 1);

            //计算向量化的物件运动时间
            var toTime = TGridCalculator.ConvertTGridToY(obj.TGrid, fumen.BpmList, 240);
            var fromTime = toTime - appearOffsetTime;
            var currentTime = MathUtils.Limit(Previewer.CurrentPlayTime, toTime, fromTime);
            var precent = (currentTime - fromTime) / appearOffsetTime;
            Log.LogDebug($"precent : {precent * 100:F2}");
            if (Previewer.CurrentPlayTime < fromTime)
                return;

            var timeX = MathUtils.CalculateXFromTwoPointFormFormula(currentTime, fromX, fromTime, toX, toTime);

            timeX = MathUtils.Limit(timeX, fromX, toX);
            var timeY = Previewer.CurrentPlayTime + Previewer.ViewHeight * (1 - precent);

            var pos = new Vector((float)timeX, (float)timeY);

            var texture = spritesMap[obj.BulletDamageTypeValue][obj.ReferenceBulletPallete.TypeValue];
            var size = spritesSize[texture];
            var origOffset = spritesOriginOffset[texture];

            Draw(texture, size, pos + origOffset, 0);
        }

        public override void Dispose()
        {
            base.Dispose();
            spritesMap.SelectMany(x => x.Value.Values).ForEach(x => x.Dispose());
            spritesMap.Clear();
        }
    }
}
