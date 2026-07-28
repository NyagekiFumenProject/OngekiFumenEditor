using Avalonia.Data.Converters;
using System.Globalization;

namespace OngekiFumenEditor.Avalonia.UI.ValueConverters;

public class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}