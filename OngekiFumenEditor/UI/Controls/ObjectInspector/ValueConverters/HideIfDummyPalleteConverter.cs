using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ValueConverters
{
    public class HideIfDummyPalleteConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.FirstOrDefault() is { } value && values.LastOrDefault() is BulletPallete pallete)
            {
                if (pallete != BulletPallete.DummyCustomPallete)
                {
                    return value?.ToString();
                }
            }

            return "--";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
