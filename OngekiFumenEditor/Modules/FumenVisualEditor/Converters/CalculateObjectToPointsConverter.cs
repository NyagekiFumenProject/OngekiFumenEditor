using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Converters
{
    public class CalculateObjectToPointsConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is FumenVisualEditorViewModel editorViewModel)
            {
                if (parameter.ToString() == "0")
                {
                    //Get X
                    if (values[1] is XGrid xGrid)
                    {
                        return XGridCalculator.ConvertXGridToX(xGrid, editorViewModel);
                    }
                }
                else
                {
                    //Get Y
                    if (values[1] is TGrid tGrid)
                    {
                        var y = TGridCalculator.ConvertTGridToY(tGrid, editorViewModel);
                        var ry = editorViewModel.TotalDurationHeight - y;
                        return ry;
                    }
                }
            }

            return 0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
