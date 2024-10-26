using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace OngekiFumenEditor.UI.ValueConverters
{
    public class LocalizeConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2)
                throw new ArgumentException("LocalizeConverter requires >=2 values");

            var strArr = values.Select(x => x.ToString()).ToArray();

            return strArr[0].Format(strArr[1..]);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
