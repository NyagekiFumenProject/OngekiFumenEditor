using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.FumenBulletPalleteListViewer.Converters
{
    public class FadeStringEnumValuesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var e = value.GetType().GetProperties(BindingFlags.Static | BindingFlags.Public)
                    .Where(x => x.CanRead && !x.CanWrite)
                    .Select(x => new
                    {
                        Name = x.Name,
                        Value = x.GetValue(null) as string
                    });

            var valueStr = value is FadeStringEnum k ? k.Value : value?.ToString();

            var r = e.FirstOrDefault(x => x.Value == valueStr).Name;
            return r;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var name = value as string;
            var val = targetType.GetProperties(BindingFlags.Static | BindingFlags.Public).Where(x => x.CanRead && !x.CanWrite).ToArray();

            var r = val.FirstOrDefault(x => x.Name == name).GetValue(null);
            return r;
        }
    }
}
