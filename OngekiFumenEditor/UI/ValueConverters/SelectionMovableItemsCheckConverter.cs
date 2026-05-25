using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using OngekiFumenEditor.Base;

namespace OngekiFumenEditor.UI.ValueConverters
{
    public class SelectionMovableItemsCheckConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var selection = value as IEnumerable<ISelectableObject>;
            return selection?.Any(s => s is OngekiMovableObjectBase) ?? false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}