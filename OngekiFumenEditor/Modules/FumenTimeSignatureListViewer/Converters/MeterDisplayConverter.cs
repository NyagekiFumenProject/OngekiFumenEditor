using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Globalization;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.FumenTimeSignatureListViewer.Converters
{
	public class MeterDisplayConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is not MeterChange met)
				return "";

			return $"{met.BunShi}/{met.Bunbo}";
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
