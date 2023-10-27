using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Converters
{
	public class UnitCloseSizeConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is null)
				return default;
			return new ComboBoxItem()
			{
				Content = value.ToString()
			};
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is ComboBoxItem item)
			{
				return double.Parse(item.Content.ToString());
			}

			return 4;
		}
	}
}
