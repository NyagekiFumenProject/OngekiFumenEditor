using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Globalization;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Converters
{
	public class BulletDisplayCalculateConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if (values[0] is not TGrid currentDisplayingTGrid ||
				values[1] is not Bullet bullet ||
				values[2] is not TGrid indicatorTGrid ||
				values[3] is not XGrid indicatorXGrid ||
				values[4] is not FumenVisualEditorViewModel editor
				)
				return 0;


			var speed = bullet.ReferenceBulletPallete.Speed;

			//todo calculate fromTGrid

			return 0;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
