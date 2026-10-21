using System;
using System.Globalization;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.FumenBulletPalleteListViewer.ValueConverters
{
    public class QuickPlacementSelectionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            => values.Length == 3 && ReferenceEquals(values[0], values[1]) &&
                values[2] is bool bell && bell == ((string)parameter == "Bell");

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
