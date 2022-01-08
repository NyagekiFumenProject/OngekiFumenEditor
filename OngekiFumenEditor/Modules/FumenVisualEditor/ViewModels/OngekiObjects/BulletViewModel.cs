using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Converters;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using static OngekiFumenEditor.Base.OngekiObjects.BulletPallete;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{
    public class BulletViewModel : DisplayObjectViewModelBase<Bullet>
    {
        public override double CheckAndAdjustX(double x)
        {
            var bullet = ReferenceOngekiObject as Bullet;
            var pallete = bullet.ReferenceBulletPallete;

            if (pallete.TargetValue == Target.Player)
            {
                x = XGridCalculator.ConvertXGridToX(XGrid.Zero, EditorViewModel);
            }
            else
            {
                x = base.CheckAndAdjustX(x);
            }

            return x;
        }
    }
}
