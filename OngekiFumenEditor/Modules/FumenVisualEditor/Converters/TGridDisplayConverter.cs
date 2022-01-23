using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Converters
{
    public class TGridDisplayConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is FumenVisualEditorViewModel editor &&
                values[1] is double judgeLineOffsetY&&
                values[2] is double minY)
            {
                return TGridCalculator.ConvertYToTGrid(minY + judgeLineOffsetY, editor)?.ToString() ?? "N/A";
            }

            return "N/A";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
