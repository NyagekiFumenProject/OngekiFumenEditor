using System;
using System.Globalization;
using System.Windows.Data;
using OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.Base.SelectionFilter;
using Xceed.Wpf.AvalonDock.Properties;
using Resources = OngekiFumenEditor.Properties.Resources;

namespace OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.ValueConverters;

public class DockableTargetSpecificationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            DockableTargetSpecification.WallLeft => Resources.WallLeft,
            DockableTargetSpecification.WallRight => Resources.WallRight,
            DockableTargetSpecification.LaneLeft => Resources.LaneLeft,
            DockableTargetSpecification.LaneCenter => Resources.LaneCenter,
            DockableTargetSpecification.LaneRight => Resources.LaneRight,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}