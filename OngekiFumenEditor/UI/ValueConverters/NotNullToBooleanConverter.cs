using System;
using System.Globalization;
using System.Windows.Data;

namespace OngekiFumenEditor.UI.ValueConverters;

public class NotNullToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}