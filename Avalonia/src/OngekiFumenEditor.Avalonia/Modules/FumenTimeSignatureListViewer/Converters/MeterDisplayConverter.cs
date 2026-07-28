using Avalonia.Data.Converters;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using System.Globalization;

namespace OngekiFumenEditor.Avalonia.Modules.FumenTimeSignatureListViewer.Converters;

public class MeterDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not MeterChange met)
            return string.Empty;

        return $"{met.BunShi}/{met.Bunbo}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
