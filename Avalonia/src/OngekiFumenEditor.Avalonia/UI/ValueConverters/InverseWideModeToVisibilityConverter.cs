using Avalonia.Data.Converters;
using System.Globalization;

namespace OngekiFumenEditor.Avalonia.UI.ValueConverters;

/// <summary>
/// Inverse of <see cref="WideModeToVisibilityConverter"/>: returns true when the input width
/// (double, e.g. ActualWidth) does not exceed the threshold (default 600, overridable via ConverterParameter).
/// </summary>
public class InverseWideModeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return !(bool)new WideModeToVisibilityConverter().Convert(value, targetType, parameter, culture);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
