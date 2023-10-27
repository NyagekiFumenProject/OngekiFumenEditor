using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OngekiFumenEditor.UI.ValueConverters
{
	public class ContrastingColorsConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is Color color)
				return Color.FromArgb(color.A, (byte)(255 - color.R), (byte)(255 - color.G), (byte)(255 - color.B));
			return default;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
