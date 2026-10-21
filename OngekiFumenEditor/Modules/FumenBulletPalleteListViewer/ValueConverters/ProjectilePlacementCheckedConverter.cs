using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Globalization;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.FumenBulletPalleteListViewer.ValueConverters
{
    public class ProjectilePlacementCheckedConverter : IMultiValueConverter
    {
        public bool IsBell { get; set; }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return values.Length == 2 &&
                values[0] is BulletPallete pallete &&
                values[1] is IPlaceBatchProjectileInfo request &&
                ReferenceEquals(request.Pallete, pallete) && request.IsBell == IsBell;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
