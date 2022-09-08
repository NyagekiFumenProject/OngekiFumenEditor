using Caliburn.Micro;
using FontStashSharp;
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
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Vector = OngekiFumenEditor.Modules.FumenPreviewer.Graphics.PrimitiveValue.Vector;
using Vector2 = System.Numerics.Vector2;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl.OngekiObjects
{
    [Export(typeof(IDrawingTarget))]
    public class BellDrawingTarget : CommonDrawTargetBase<Bell>, IDisposable
    {
        private Texture texture;
        private OpenTK.Mathematics.Vector2 size;

        private IStringDrawing stringDrawing;
        private ITextureDrawing textureDrawing;

        public override IEnumerable<string> DrawTargetID { get; } = new[] { Bell.CommandName };

        public BellDrawingTarget() : base()
        {
            var info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\bell.png", UriKind.Relative));
            using var bitmap = Image.FromStream(info.Stream) as Bitmap;
            texture = new Texture(bitmap);
            size = new(40, 40);
            stringDrawing = IoC.Get<IStringDrawing>();
            textureDrawing = IoC.Get<ITextureDrawing>();
        }

        public float CalculateBulletMsecTime(IFumenPreviewer target, Bell obj, float userSpeed = 2.35f)
        {
            //const float fat = 3.95f;
            //var time =  32.5f * fat / (Math.Max(4.7f, 0.2f * userSpeed) * (/*obj.ReferenceBulletPallete?.Speed ??*/ 1f)) * 16.666666f;
            var time = (float)target.ViewHeight / (obj.ReferenceBulletPallete?.Speed ?? 1f);
            return time;
        }

        public override void Draw(IFumenPreviewer target, Bell obj)
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

            var toTime = TGridCalculator.ConvertTGridToY(obj.TGrid, target.Fumen.BpmList, 1, 240);
            var fromTime = toTime - appearOffsetTime;
            var currentTime = MathUtils.Limit(target.CurrentPlayTime, toTime, fromTime);
            if (target.CurrentPlayTime < fromTime)
                return;
            var precent = (currentTime - fromTime) / appearOffsetTime;

            var timeX = MathUtils.CalculateXFromTwoPointFormFormula(currentTime, fromX, fromTime, toX, toTime);

            timeX = MathUtils.Limit(timeX, fromX, toX);
            var timeY = target.CurrentPlayTime + target.ViewHeight * (1 - precent);

            var pos = new Vector((float)timeX, (float)timeY);

            textureDrawing.Draw(target, texture, new (Vector2, Vector2, float)[] { (new(size.X, size.Y), new(pos.X, pos.Y), 0f) });

            DrawPallateStr(target, obj, pos);
            //RegisterHitTest(obj, new() { X = pos.X - (size.X / 2), Y = pos.Y - (size.Y / 2), Width = size.X, Height = size.Y });
        }

        private void DrawPallateStr(IFumenPreviewer target, IBulletPalleteReferencable obj, Vector pos)
        {
            if (obj.ReferenceBulletPallete is null)
                return;
            stringDrawing.Draw($"{obj.ReferenceBulletPallete.StrID}", new(pos.X - target.ViewWidth / 2, pos.Y + 5), Vector2.One, 16, 0, new(1, 0, 0, 1), new(0.5f, 0.5f), IStringDrawing.StringStyle.Normal, target, default, out _);
        }

        public void Dispose()
        {
            texture?.Dispose();
            texture = null;
        }
    }
}
