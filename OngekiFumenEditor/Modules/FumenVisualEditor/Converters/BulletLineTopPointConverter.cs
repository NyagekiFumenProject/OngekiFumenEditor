using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using static OngekiFumenEditor.Base.OngekiObjects.BulletPallete;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Converters
{
    public class BulletLineTopPointConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is not FumenVisualEditorViewModel editor || values[1] is not BulletAuxiliaryLine line)
                return 0;

            //计算此bullet理论辅助线
            var bullet = line.From;
            var pallete = bullet.ReferenceBulletPallete;

            if (parameter.ToString() == "0")
            {
                var xUnit = 0f;

                //暂时实现Shooter.TargetHead && Target.FixField的
                if (pallete.ShooterValue == Shooter.TargetHead)
                {
                    if (pallete.TargetValue == Target.FixField)
                    {
                        xUnit = bullet.XGrid.Unit;
                    }
                }

                xUnit += pallete.PlaceOffset;
                var x = XGridCalculator.ConvertXGridToX(new XGrid(xUnit), editor);
                return x;
            }
            else
            {
                return 0.0;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
