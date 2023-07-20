using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.OgkiFumenListBrowser.ValueConverters
{
    internal class LoadJacketConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string filePath)
                return null;

            if (!File.Exists(filePath))
                return null;

            if (Path.GetExtension(filePath).ToLower() switch
            {
                ".png" or ".jpg" or "jpeg" => true,
                _ => false
            })
                return filePath;



            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
