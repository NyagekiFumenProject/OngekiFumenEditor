using Caliburn.Micro;
using FontStashSharp;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Base;
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
using static System.Windows.Forms.AxHost;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    public class BulletDrawingTarget : CommonBatchDrawTargetBase<Bullet>, IDisposable
    {
        public override int DefaultRenderOrder => 1500;

        Dictionary<BulletDamageType, Dictionary<BulletType, Texture>> spritesMap = new();
        Dictionary<Texture, Vector2> spritesSize = new();
        Dictionary<Texture, Vector2> spritesOriginOffset = new();

        Dictionary<Texture, List<(Vector2, Vector2, float)>> normalDrawList = new();
        Dictionary<Texture, List<(Vector2, Vector2, float)>> selectedDrawList = new();

        private List<(Vector2 pos, IBulletPalleteReferencable obj)> drawStrList = new();
        private IStringDrawing stringDrawing;
        private IHighlightBatchTextureDrawing highlightDrawing;
        private IBatchTextureDrawing batchTextureDrawing;

        public override IEnumerable<string> DrawTargetID { get; } = new[] { Bullet.CommandName };

        public BulletDrawingTarget() : base()
        {
            Texture LoadTex(string rPath)
            {
                var info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\" + rPath, UriKind.Relative));
                using var bitmap = Image.FromStream(info.Stream) as Bitmap;
                return new Texture(bitmap);
            }

            void SetTexture(BulletDamageType k1, BulletType k2, string rPath, Vector2 size, Vector2 origOffset)
            {
                if (!spritesMap.TryGetValue(k1, out var dic))
                {
                    dic = new Dictionary<BulletType, Texture>();
                    spritesMap[k1] = dic;
                }

                var tex = LoadTex(rPath);
                dic[k2] = tex;
                normalDrawList[tex] = new();
                selectedDrawList[tex] = new();

                spritesSize[tex] = size;
                spritesOriginOffset[tex] = origOffset;
            }

            var size = new Vector2(40, 40);
            var origOffset = new Vector2(0, 0);
            SetTexture(BulletDamageType.Normal, BulletType.Circle, "nt_mine_red.png", size, origOffset);
            SetTexture(BulletDamageType.Hard, BulletType.Circle, "nt_mine_pur.png", size, origOffset);
            SetTexture(BulletDamageType.Danger, BulletType.Circle, "nt_mine_blk.png", size, origOffset);

            size = new(30, 80);
            origOffset = new Vector2(0, 35);
            SetTexture(BulletDamageType.Normal, BulletType.Needle, "tri_bullet0.png", size, origOffset);
            SetTexture(BulletDamageType.Hard, BulletType.Needle, "tri_bullet1.png", size, origOffset);
            SetTexture(BulletDamageType.Danger, BulletType.Needle, "tri_bullet2.png", size, origOffset);

            size = new(30, 80);
            origOffset = new Vector2(0, 35);
            SetTexture(BulletDamageType.Normal, BulletType.Square, "sqrt_bullet0.png", size, origOffset);
            SetTexture(BulletDamageType.Hard, BulletType.Square, "sqrt_bullet1.png", size, origOffset);
            SetTexture(BulletDamageType.Danger, BulletType.Square, "sqrt_bullet2.png", size, origOffset);

            stringDrawing = IoC.Get<IStringDrawing>();
            batchTextureDrawing = IoC.Get<IBatchTextureDrawing>();
            highlightDrawing = IoC.Get<IHighlightBatchTextureDrawing>();
        }

        public float CalculateBulletMsecTime(IFumenEditorDrawingContext target, Bullet obj, float userSpeed = 2.35f)
        {
            //const float fat = 3.95f;
            //var time =  32.5f * fat / (Math.Max(4.7f, 0.2f * userSpeed) * (/*obj.ReferenceBulletPallete?.Speed ??*/ 1f)) * 16.666666f;
            var time = (float)target.ViewHeight / (obj.ReferenceBulletPallete?.Speed ?? 1f);
            return time;
        }

        private void Draw(IFumenEditorDrawingContext target, Bullet obj)
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

            var fromX = XGridCalculator.ConvertXGridToX(obj.ReferenceBulletPallete?.CalculateFromXGrid(obj.XGrid, target.Editor.Fumen) ?? obj.XGrid, target.Editor.Setting.XGridDisplayMaxUnit, target.ViewWidth, 1);
            var toX = XGridCalculator.ConvertXGridToX(obj.ReferenceBulletPallete?.CalculateToXGrid(obj.XGrid, target.Editor.Fumen) ?? obj.XGrid, target.Editor.Setting.XGridDisplayMaxUnit, target.ViewWidth, 1);

            //计算向量化的物件运动时间
            var toTime = TGridCalculator.ConvertTGridToY_DesignMode(obj.TGrid, target.Editor);
            var fromTime = toTime - appearOffsetTime;
            var currentTime = target.CurrentPlayTime;
            var precent = (currentTime - fromTime) / appearOffsetTime;
            //Log.LogDebug($"precent : {precent * 100:F2}");
            if (target.CurrentPlayTime < fromTime)
                return;

            var timeX = MathUtils.CalculateXFromTwoPointFormFormula(currentTime, fromX, fromTime, toX, toTime);

            var timeY = target.Rect.MinY + target.Rect.Height * (1 - precent) + target.Editor.Setting.JudgeLineOffsetY;

            var pos = new Vector2((float)timeX, (float)timeY);

            var texture = spritesMap[obj.BulletDamageTypeValue][obj.ReferenceBulletPallete.TypeValue];
            var size = spritesSize[texture];
            var origOffset = spritesOriginOffset[texture];

            var rotate = (float)Math.Atan((toX - fromX) / (toTime - fromTime));
            var offsetPos = pos + origOffset;
            normalDrawList[texture].Add((size, offsetPos, rotate));
            if (obj.IsSelected)
                selectedDrawList[texture].Add((size * 1.3f, offsetPos, rotate));
            drawStrList.Add((offsetPos, obj));
            target.RegisterSelectableObject(obj, offsetPos, size);
        }


        private void DrawEditor(IFumenEditorDrawingContext target, Bullet obj)
        {
            var toX = XGridCalculator.ConvertXGridToX(obj.ReferenceBulletPallete?.CalculateToXGrid(obj.XGrid, target.Editor.Fumen) ?? obj.XGrid, target.Editor.Setting.XGridDisplayMaxUnit, target.ViewWidth, 1);
            var toTime = TGridCalculator.ConvertTGridToY_DesignMode(obj.TGrid, target.Editor);
            
            var pos = new Vector2((float)toX, (float)toTime);

            var texture = spritesMap[obj.BulletDamageTypeValue][obj.ReferenceBulletPallete.TypeValue];
            var size = spritesSize[texture];
            var origOffset = spritesOriginOffset[texture];

            var offsetPos = pos + origOffset;
            normalDrawList[texture].Add((size, offsetPos, 0));
            if (obj.IsSelected)
                selectedDrawList[texture].Add((size * 1.3f, offsetPos, 0));
            drawStrList.Add((offsetPos, obj));
            target.RegisterSelectableObject(obj, offsetPos, size);
        }

        private void DrawPallateStr(IDrawingContext target)
        {
            foreach ((var pos, var obj) in drawStrList)
            {
                if (obj.ReferenceBulletPallete is null)
                    return;
                stringDrawing.Draw($"{obj.ReferenceBulletPallete.StrID}", new(pos.X, pos.Y + 5), Vector2.One, 16, 0, Vector4.One, new(0.5f, 0.5f), default, target, default, out _);
            }
        }

        public void Dispose()
        {
            spritesMap.SelectMany(x => x.Value.Values).ForEach(x => x.Dispose());
            spritesMap.Clear();
            ClearDrawList();
        }

        private void ClearDrawList()
        {
            foreach (var l in normalDrawList.Values)
                l.Clear();
            foreach (var l in selectedDrawList.Values)
                l.Clear();
            drawStrList.Clear();
        }

        public override void DrawBatch(IFumenEditorDrawingContext target, IEnumerable<Bullet> objs)
        {
            if (target.Editor.IsDesignMode)
            {
                foreach (var obj in objs)
                    DrawEditor(target, obj);
            }
            else
            {
                foreach (var obj in objs)
                    Draw(target, obj);
            }

            foreach (var item in selectedDrawList)
                highlightDrawing.Draw(target, item.Key, item.Value);

            foreach (var item in normalDrawList)
                batchTextureDrawing.Draw(target, item.Key, item.Value);

            if (target.Editor.IsDesignMode)
                DrawPallateStr(target);
            ClearDrawList();
        }
    }
}
