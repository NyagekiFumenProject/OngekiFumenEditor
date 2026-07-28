using Avalonia.Data.Converters;
using System.Globalization;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ValueConverters;

public class HideIfDummyPalleteConverter : IValueConverter
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