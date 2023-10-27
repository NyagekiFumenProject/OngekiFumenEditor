using System;
using System.Globalization;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Converters
{
	public interface IBulletLineButtomPointConverter
	{
		object Convert(object[] values, Type targetType, object parameter, CultureInfo culture);
		object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture);
	}
}