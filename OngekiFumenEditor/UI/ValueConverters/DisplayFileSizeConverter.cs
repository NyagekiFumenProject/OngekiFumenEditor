using OngekiFumenEditor.Utils;
using SvgConverter;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace OngekiFumenEditor.UI.ValueConverters
{
    public class DisplayFileSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!long.TryParse(value?.ToString(), out var size))
                return null;
            return FileHelper.FormatFileSize(size);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
