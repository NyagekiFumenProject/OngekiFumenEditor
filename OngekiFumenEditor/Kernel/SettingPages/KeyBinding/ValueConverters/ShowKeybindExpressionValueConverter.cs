using OngekiFumenEditor.Kernel.KeyBinding;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;

namespace OngekiFumenEditor.Kernel.SettingPages.KeyBinding.ValueConverters
{
    public class ShowKeybindExpressionValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.FirstOrDefault() is Key key && values.LastOrDefault() is ModifierKeys modifier)
                return KeyBindingDefinition.FormatToExpression(key, modifier);

            return string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
