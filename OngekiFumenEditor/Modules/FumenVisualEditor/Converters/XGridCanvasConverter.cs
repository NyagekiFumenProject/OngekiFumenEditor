using OngekiFumenEditor.Base;
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
    public class XGridCanvasConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.ElementAtOrDefault(0) is float xgridUnit
                &&
                values.ElementAtOrDefault(1) is FumenVisualEditorViewModel modelView)
            {
                var x = xgridUnit * (modelView.XUnitSize / modelView.Setting.UnitCloseSize) + modelView.CanvasWidth / 2;
                return x;
            }

            return 0d;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
