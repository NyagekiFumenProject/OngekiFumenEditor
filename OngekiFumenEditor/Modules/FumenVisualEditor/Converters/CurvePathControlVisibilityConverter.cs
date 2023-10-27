using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Converters
{
	public class CurvePathControlVisibilityConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if (values[0] is not Visibility editor || values[1] is not Visibility parent || values[2] is not Visibility otherControls)
				return Visibility.Hidden;

			var result = editor == Visibility.Visible && (parent == Visibility.Visible || otherControls == Visibility.Visible) ? Visibility.Visible : Visibility.Hidden;

			//Log.LogDebug($"{editor} && ({parent} || {otherControls}) -> {result}");
			return result;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
