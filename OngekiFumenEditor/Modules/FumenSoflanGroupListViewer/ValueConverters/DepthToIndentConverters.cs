using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.ValueConverters
{
    /// <summary>抵消 TreeView 的层级缩进，让不同深度的开关列对齐到同一全局位置。</summary>
    internal sealed class DepthToControlMarginConverter : IValueConverter
    {
        private const double IndentPerLevel = 36;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var depth = value is int d ? d : 0;
            return new Thickness(-IndentPerLevel * depth, 0, 0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>为名称列留出被负边距占用的缩进空间，避免文字与开关重叠。</summary>
    internal sealed class DepthToNameMarginConverter : IValueConverter
    {
        private const double IndentPerLevel = 36;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var depth = value is int d ? d : 0;
            return new Thickness(0, 0, IndentPerLevel * depth, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
