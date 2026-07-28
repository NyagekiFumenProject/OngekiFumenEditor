using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Converters
{
	public class IsStringEmptyConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return string.IsNullOrWhiteSpace(value?.ToString());
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}

