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
            if (values[0] is not FumenVisualEditorViewModel editor ||
                values[1] is not BulletPalleteAuxiliaryLine line)
                return 0d;

            //计算此bullet理论辅助线
            var refObject = line.From;
            var pallete = refObject.ReferenceBulletPallete;

            if (pallete == null)
                return 0d;

            if (parameter.ToString() == "0")
            {
                var xUnit = 0f;

                //暂时实现Shooter.TargetHead && Target.FixField的
                if (pallete.ShooterValue == Shooter.TargetHead &
                    pallete.TargetValue == Target.FixField)
                {
                    xUnit = refObject.XGrid.Unit;
                }

                xUnit += pallete.PlaceOffset;
                var x = XGridCalculator.ConvertXGridToX(new XGrid(xUnit), editor);
                return x;
            }
            else
            {
                var canvasY = TGridCalculator.ConvertTGridToY(refObject.TGrid, editor);
                return editor.TotalDurationHeight - (canvasY + editor.ViewHeight);
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
