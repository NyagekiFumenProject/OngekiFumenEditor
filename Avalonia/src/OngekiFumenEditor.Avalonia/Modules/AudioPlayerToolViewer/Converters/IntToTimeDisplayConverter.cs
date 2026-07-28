using Avalonia.Data.Converters;
using System.Globalization;

namespace OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Converters;

public class IntToTimeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var timeSpan = (TimeSpan)(value is float f ? TimeSpan.FromMilliseconds(f) : value);
        return $"{timeSpan.Minutes}:{timeSpan.Seconds}.{timeSpan.Milliseconds}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
