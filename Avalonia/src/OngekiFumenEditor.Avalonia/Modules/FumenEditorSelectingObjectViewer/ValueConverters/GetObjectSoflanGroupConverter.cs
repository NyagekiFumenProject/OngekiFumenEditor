using Avalonia.Data.Converters;
using System.Globalization;

namespace OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.ValueConverters;

public class GetObjectSoflanGroupConverter : IValueConverter
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