using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.PrimitiveValue;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl
{
    [Export(typeof(IDrawingTarget))]
    public class BellDrawingTarget : CommonSpriteDrawTargetBase<Bell>
    {
        public override IEnumerable<string> DrawTargetID { get; } = new[] { Bell.CommandName  };

        public BellDrawingTarget() : base(new Texture(Properties.Resources.bell))
        {

        }

        public float CalculateBulletMsecTime(Bell obj,float userSpeed = 2.35f)
        {
            const float fat = 3.95f;
            return 32.5f * fat / (Math.Max(4.7f, 0.2f * userSpeed) * (obj.ReferenceBulletPallete?.Speed ?? 1f)) * 16.666666f;
        }

        protected override Vector? GetObjectPosition(Bell obj, OngekiFumen fumen)
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

            var toTime = TGridCalculator.ConvertTGridToY(obj.TGrid, fumen.BpmList, 240);
            var fromTime = toTime - appearOffsetTime;
            var currentTime = MathUtils.Limit(Previewer.CurrentPlayTime, toTime, fromTime);
            if (Previewer.CurrentPlayTime < fromTime)
                return null;
            var precent = (currentTime - fromTime) / appearOffsetTime;

            var timeX = MathUtils.CalculateXFromTwoPointFormFormula(currentTime, fromX, fromTime, toX, toTime);

            timeX = MathUtils.Limit(timeX, fromX, toX);
            var timeY = Previewer.CurrentPlayTime + Previewer.ViewHeight * (1 - precent);

            return new((float)timeX, (float)timeY);
        }
    }
}
