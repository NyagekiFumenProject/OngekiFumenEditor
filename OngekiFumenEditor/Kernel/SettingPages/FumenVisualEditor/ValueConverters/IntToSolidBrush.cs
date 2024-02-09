using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OngekiFumenEditor.Kernel.SettingPages.FumenVisualEditor.ValueConverters
{
    public class IntToSolidBrush : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not int argb)
                return default;
            var isReverse = parameter is string b ? (bool.TryParse(b, out var w) ? w : false) : false;
            var color = System.Drawing.Color.FromArgb(argb);
            var acolor = isReverse ? System.Drawing.Color.FromArgb(255, 255 - color.R, 255 - color.G, 255 - color.B) : color;
            var r = new SolidColorBrush(System.Windows.Media.Color.FromArgb(acolor.A, acolor.R, acolor.G, acolor.B));
            return r;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
