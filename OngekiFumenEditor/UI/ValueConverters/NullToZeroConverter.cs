using System;
using System.Globalization;
using System.Windows.Data;

namespace OngekiFumenEditor.UI.ValueConverters;

public class NullToZeroConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
            return 0;
        else
            return parameter;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}