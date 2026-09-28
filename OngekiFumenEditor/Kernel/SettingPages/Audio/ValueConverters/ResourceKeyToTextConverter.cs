using OngekiFumenEditor.Properties;
using System;
using System.Globalization;
using System.Windows.Data;

namespace OngekiFumenEditor.Kernel.SettingPages.Audio.ValueConverters
{
    /// <summary>
    /// 把资源键（对本仓库的设置项而言即属性名，例如 <c>WaveformBackgroundColor</c>）转换为本地化文本，
    /// 查不到资源时原样返回。设置项在 <c>*.resx</c> 中以属性名作为键。
    /// </summary>
    public class ResourceKeyToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string resourceKey || resourceKey.Length == 0)
                return value;

            return Resources.ResourceManager.GetString(resourceKey) ?? resourceKey;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
