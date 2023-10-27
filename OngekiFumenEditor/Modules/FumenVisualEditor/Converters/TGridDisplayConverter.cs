using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Globalization;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Converters
{
	public class TGridDisplayConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if (values[0] is FumenVisualEditorViewModel editor &&
				values[1] is TGrid tGrid)
			{
				if (tGrid is not null)
				{
					if (editor.Setting.DisplayTimeFormat == Models.EditorSetting.TimeFormat.TGrid)
						return tGrid.ToString();

					var audioTime = TGridCalculator.ConvertTGridToAudioTime(tGrid, editor);
					return $"{audioTime.Minutes,-2}:{audioTime.Seconds,-2}:{audioTime.Milliseconds,-3}";
				}
			}

			return "N/A";
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
