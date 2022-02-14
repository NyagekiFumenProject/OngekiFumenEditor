using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Converters
{
    public class BeatRhythmToVisibleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is int beatRhythm && values[1] is Visibility visible)
            {
                if (beatRhythm == 0)
                    return Visibility.Visible;
                return visible;
            }

            return Visibility.Visible;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
