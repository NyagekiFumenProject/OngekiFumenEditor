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
            if (values?.OfType<FumenVisualEditorViewModel>()?.FirstOrDefault() is FumenVisualEditorViewModel modelView 
                &&
                values?.OfType<float>()?.FirstOrDefault() is float xgridUnit)
            {
                var x = xgridUnit * (modelView.XUnitSize / modelView.UnitCloseSize) + modelView.CanvasWidth / 2;
                return x;
            }

            return 0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
