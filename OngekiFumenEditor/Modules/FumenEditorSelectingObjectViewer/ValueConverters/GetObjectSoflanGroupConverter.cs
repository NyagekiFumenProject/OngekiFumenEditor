using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.ValueConverters
{
    public class GetObjectSoflanGroupConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.ElementAtOrDefault(0) is OngekiObjectBase obj && values.ElementAtOrDefault(1) is FumenVisualEditorViewModel editor)
            {
                editor._cacheSoflanGroupRecorder.GetCache(obj, out var soflanGroup);
                return soflanGroup;
            }

            return default;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
