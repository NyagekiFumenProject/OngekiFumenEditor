using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OngekiFumenEditor.UI.ValueConverters
{
	public class ReverseBoolToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var falseEnum = (bool.TryParse(parameter?.ToString(), out var r) && r) ? Visibility.Collapsed : Visibility.Hidden;
			return (value is bool b ? b : false) ? falseEnum : Visibility.Visible;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
