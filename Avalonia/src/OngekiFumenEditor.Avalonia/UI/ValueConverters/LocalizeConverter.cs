using Avalonia.Data.Converters;
using System.Globalization;

namespace OngekiFumenEditor.Avalonia.UI.ValueConverters;

public class LocalizeConverter : IMultiValueConverter
{
    public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Count < 2)
            throw new ArgumentException("LocalizeConverter requires >=2 values");

        var stringValues = values.Select(static value => value?.ToString() ?? string.Empty).ToArray();
        return string.Format(culture, stringValues[0], stringValues[1..]);
    }
}
