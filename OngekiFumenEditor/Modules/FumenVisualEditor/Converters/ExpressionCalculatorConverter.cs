using ExtrameFunctionCalculator;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Converters
{
    class ExpressionCalculatorConverter : IMultiValueConverter
    {
        public Calculator calculator = new Calculator();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            for (int i = 0; i < values.Length; i++)
            {
                calculator.SetExpressionVariable($"x{(i == 0 ? "":i.ToString())}={values[i]}");
            }

            if (parameter is string exprStr && !string.IsNullOrWhiteSpace(exprStr))
            {
                return calculator.Solve(exprStr);
            }

            return default;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
