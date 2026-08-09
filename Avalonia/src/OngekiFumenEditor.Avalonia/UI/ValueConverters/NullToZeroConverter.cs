using Avalonia.Data.Converters;
using System.Globalization;

namespace OngekiFumenEditor.Avalonia.UI.ValueConverters;

public class NullToZeroConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is null ? 0d : parameter;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
