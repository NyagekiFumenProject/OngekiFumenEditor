using System;
using System.Globalization;
using System.Windows.Data;

namespace OngekiFumenEditor.UI.ValueConverters
{
	public class LambdaConverter<T, X> : IValueConverter
	{
		private readonly Func<T, Type, object, CultureInfo, X> func;

		public LambdaConverter(Func<T, X> func2)
		{
			func = (a, _, __, ___) => func2(a);
		}

		public LambdaConverter(Func<T, Type, object, CultureInfo, X> func)
		{
			this.func = func;
		}

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return func((T)value, targetType, parameter, culture);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
