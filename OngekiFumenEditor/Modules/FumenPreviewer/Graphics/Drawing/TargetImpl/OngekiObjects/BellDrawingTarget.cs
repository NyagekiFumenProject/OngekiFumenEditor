using Caliburn.Micro;
using FontStashSharp;
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
using Vector2 = System.Numerics.Vector2;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl.OngekiObjects
{
    [Export(typeof(IDrawingTarget))]
    public class BellDrawingTarget : CommonBatchDrawTargetBase<Bell>, IDisposable
    {
        private Texture texture;
        private Vector2 size;

        private IStringDrawing stringDrawing;
        private IBatchTextureDrawing textureDrawing;

        public override IEnumerable<string> DrawTargetID { get; } = new[] { Bell.CommandName };

        public BellDrawingTarget() : base()
        {
            texture = ResourceUtils.OpenReadTextureFromResource(@"Modules\FumenVisualEditor\Views\OngekiObjects\bell.png");
            size = new(40, 40);
            stringDrawing = IoC.Get<IStringDrawing>();
            textureDrawing = IoC.Get<IBatchTextureDrawing>();
        }

        public float CalculateBulletMsecTime(IFumenPreviewer target, Bell obj, float userSpeed = 2.35f)
        {
            //const float fat = 3.95f;
            //var time =  32.5f * fat / (Math.Max(4.7f, 0.2f * userSpeed) * (/*obj.ReferenceBulletPallete?.Speed ??*/ 1f)) * 16.666666f;
            var time = (float)target.ViewHeight / (obj.ReferenceBulletPallete?.Speed ?? 1f);
            return time;
        }

        public void PostDraw(IFumenPreviewer target, Bell obj)
        {
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
            var appearOffsetTime = CalculateBulletMsecTime(target, obj);

            var toTime = TGridCalculator.ConvertTGridToY(obj.TGrid, target.Fumen.BpmList, 1, 240);
            var fromTime = toTime - appearOffsetTime;

            if (target.CurrentPlayTime < fromTime)
                return;

            var fromX = XGridCalculator.ConvertXGridToX(obj.ReferenceBulletPallete?.CalculateFromXGrid(obj.XGrid.TotalUnit, target.Fumen) ?? obj.XGrid.TotalUnit, 30, target.ViewWidth, 1);
            var toX = XGridCalculator.ConvertXGridToX(obj.ReferenceBulletPallete?.CalculateToXGrid(obj.XGrid.TotalUnit, target.Fumen) ?? obj.XGrid.TotalUnit, 30, target.ViewWidth, 1);

            var currentTime = MathUtils.Limit(target.CurrentPlayTime, toTime, fromTime);

            var precent = (currentTime - fromTime) / appearOffsetTime;

            var timeX = MathUtils.CalculateXFromTwoPointFormFormula(currentTime, fromX, fromTime, toX, toTime);

            timeX = MathUtils.Limit(timeX, fromX, toX);
            var timeY = target.CurrentPlayTime + target.ViewHeight * (1 - precent);

            var pos = new Vector2((float)timeX, (float)timeY);
            textureDrawing.PostSprite(size, pos, 0f);
            DrawPallateStr(target, obj, pos);
            target.RegisterSelectableObject(obj, pos, size);
        }

        public override void DrawBatch(IFumenPreviewer target, IEnumerable<Bell> objs)
        {
            textureDrawing.Begin(target, texture);
            foreach (var obj in objs)
                PostDraw(target, obj);
            textureDrawing.End();

            //RegisterHitTest(obj, new() { X = pos.X - (size.X / 2), Y = pos.Y - (size.Y / 2), Width = size.X, Height = size.Y });
        }

        private void DrawPallateStr(IFumenPreviewer target, IBulletPalleteReferencable obj, Vector2 pos)
        {
            if (obj.ReferenceBulletPallete is null)
                return;
            stringDrawing.Draw($"{obj.ReferenceBulletPallete.StrID}", new(pos.X, pos.Y + 5), Vector2.One, 16, 0, new(1, 0, 0, 1), new(0.5f, 0.5f), IStringDrawing.StringStyle.Normal, target, default, out _);
        }

        public void Dispose()
        {
            texture?.Dispose();
            texture = null;
        }
    }
}
