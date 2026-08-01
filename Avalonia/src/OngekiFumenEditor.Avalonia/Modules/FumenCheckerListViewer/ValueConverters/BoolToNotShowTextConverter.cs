using Avalonia.Data.Converters;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using System.Globalization;

namespace OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.ValueConverters;

/// <summary>
/// 替代 WPF 版 FumenCheckerListViewerView 中 Run.Style 的 DataTrigger：
/// 值为 true（正在显示该类检查结果）时返回空串，否则返回本地化的 [NotShow] 后缀文本。
/// </summary>
public class BoolToNotShowTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return string.Empty;
        return Lang.LocalizerManager.GetLocalizedText("NotShow") ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
