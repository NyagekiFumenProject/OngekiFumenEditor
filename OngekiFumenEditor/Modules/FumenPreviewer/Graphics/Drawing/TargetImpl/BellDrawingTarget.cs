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

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl
{
    [Export(typeof(IDrawingTarget))]
    public class BellDrawingTarget : CommonSpriteDrawTargetBase<Bell>
    {
        private Texture texture;
        private Vector2 size;

        public override IEnumerable<string> DrawTargetID { get; } = new[] { Bell.CommandName };

        public BellDrawingTarget() : base()
        {
            var info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\bell.png", UriKind.Relative));
            using var bitmap = Image.FromStream(info.Stream) as Bitmap;
            texture = new Texture(bitmap);
            size = new Vector2(40, 40);
        }

        public float CalculateBulletMsecTime(Bell obj, float userSpeed = 2.35f)
        {
            //const float fat = 3.95f;
            //var time =  32.5f * fat / (Math.Max(4.7f, 0.2f * userSpeed) * (/*obj.ReferenceBulletPallete?.Speed ??*/ 1f)) * 16.666666f;
            var time = (float)Previewer.ViewHeight / (obj.ReferenceBulletPallete?.Speed ?? 1f);
            return time;
        }

        public override void Draw(Bell obj, OngekiFumen fumen)
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

            var toTime = TGridCalculator.ConvertTGridToY(obj.TGrid, fumen.BpmList, 1, 240);
            var fromTime = toTime - appearOffsetTime;
            var currentTime = MathUtils.Limit(Previewer.CurrentPlayTime, toTime, fromTime);
            if (Previewer.CurrentPlayTime < fromTime)
                return;
            var precent = (currentTime - fromTime) / appearOffsetTime;

            var timeX = MathUtils.CalculateXFromTwoPointFormFormula(currentTime, fromX, fromTime, toX, toTime);

            timeX = MathUtils.Limit(timeX, fromX, toX);
            var timeY = Previewer.CurrentPlayTime + Previewer.ViewHeight * (1 - precent);

            var pos = new Vector((float)timeX, (float)timeY);

            Draw(texture, size, pos, 0);
        }

        public override void Dispose()
        {
            base.Dispose();
            texture?.Dispose();
            texture = null;
        }
    }
}
