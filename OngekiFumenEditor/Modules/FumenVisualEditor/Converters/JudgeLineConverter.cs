using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Globalization;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Converters
{
	public class JudgeLineConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if (values[0] is FumenVisualEditorViewModel editor &&
				values[1] is double judgeLineOffsetY)
			{
				return editor.ViewHeight / 2 - judgeLineOffsetY + (parameter is not null ? (double.TryParse(parameter.ToString(), out var d) ? d : 0) : 0);
			}

			return 0;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
