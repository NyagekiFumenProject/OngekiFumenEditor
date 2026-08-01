using Avalonia.Data.Converters;
using System.Globalization;

namespace OngekiFumenEditor.Avalonia.UI.ValueConverters;

/// <summary>
/// Returns true when the input width (double, e.g. ActualWidth) exceeds the threshold,
/// i.e. the panel should use its "wide" layout.
/// The threshold defaults to 600 and can be overridden via ConverterParameter.
/// </summary>
public class WideModeToVisibilityConverter : IValueConverter
{
    public const double DefaultThreshold = 600d;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var threshold = DefaultThreshold;
        if (parameter is not null && double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            threshold = parsed;

        return value is double width && width > threshold;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
