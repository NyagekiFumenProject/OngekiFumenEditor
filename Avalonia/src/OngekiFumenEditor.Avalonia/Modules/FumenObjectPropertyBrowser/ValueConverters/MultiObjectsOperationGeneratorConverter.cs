using Avalonia;
using Avalonia.Data.Converters;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.UIGenerator;
using System.Globalization;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ValueConverters;

public class MultiObjectsOperationGeneratorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not IReadOnlySet<ISelectableObject> objects || objects.Count <= 1)
            return AvaloniaProperty.UnsetValue;

        return OngekiMultiObjectsOperationGenerator.GenerateUI(objects.OfType<OngekiObjectBase>()) ?? AvaloniaProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return AvaloniaProperty.UnsetValue;
    }
}
