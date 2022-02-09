using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using static OngekiFumenEditor.Modules.SvgToLaneBrowser.ViewModels.SvgToLaneBrowserViewModel;

namespace OngekiFumenEditor.Modules.SvgToLaneBrowser.Converters
{
    public class PathVisibleConverter : IMultiValueConverter
    {
        /*
         * <Binding Path="LaneTarget" />
           <Binding ElementName="PathCanvas" Path="DataContext.IsShowOutputableOnly" />
         */
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[1] is bool isOnlyShow)
            {
                if (!isOnlyShow)
                    return Visibility.Visible;

                return values[0] switch
                {
                    LaneType.Left or LaneType.Center or LaneType.Right => Visibility.Visible,
                    _ => Visibility.Hidden
                };
            }

            return Visibility.Visible;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
