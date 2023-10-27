using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using System;
using System.Globalization;
using System.Windows.Data;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ValueConverters
{
	public class PropertyGeneratorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is not IObjectPropertyAccessProxy wrapper)
				return default;
			return PropertiesUIGenerator.GenerateUI(wrapper);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
