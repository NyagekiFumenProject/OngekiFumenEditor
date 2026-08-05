using Avalonia.Data.Converters;
using Avalonia.Input;
using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;
using System.Globalization;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.KeyBinding.ValueConverters;

public sealed class ShowKeybindExpressionValueConverter : IMultiValueConverter
{
    public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Count >= 2 && values[0] is Key key && values[1] is KeyModifiers modifiers)
            return KeyBindingDefinition.FormatToExpression(key, modifiers);

        return string.Empty;
    }
}
