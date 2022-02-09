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
    public class PathStrokeConverter : IMultiValueConverter
    {
        /*
                                        <Binding Path="LaneTargetColor" />
                                        <Binding Path="LaneTarget" />
                                        <Binding Path="Color" />
                                        <Binding ElementName="PathCanvas" Path="DataContext.IsShowLaneColor" />
         */
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is Brush laneBrush && values[3] is bool isShowLaneColor)
            {
                if (!isShowLaneColor)
                    return values[2];

                return values[1] switch
                {
                    LaneType.Left or LaneType.Center or LaneType.Right => laneBrush,
                    _ => Visibility.Hidden
                };
            }

            return values[2];
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
