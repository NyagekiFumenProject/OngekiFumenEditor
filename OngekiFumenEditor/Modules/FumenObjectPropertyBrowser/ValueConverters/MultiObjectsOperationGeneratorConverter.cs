using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ValueConverters
{
	public class MultiObjectsOperationGeneratorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is not IReadOnlySet<ISelectableObject> objs)
				return default;
			if (objs.Count <= 1)
				return default;
			return OngekiMultiObjectsOperationGenerator.GenerateUI(objs.OfType<OngekiObjectBase>());
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
