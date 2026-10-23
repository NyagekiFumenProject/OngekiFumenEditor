using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models;
using System.Numerics;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.ViewModels
{
    /// <summary>把稳定的组色转成可复用的冻结画刷，供组树与选择对话框共用。</summary>
    public static class SoflanGroupColorBrush
    {
        public static Brush Create(int soflanGroup)
        {
            var color = SoflanGroupColor.Get(soflanGroup);
            var brush = new SolidColorBrush(ToColor(color));
            brush.Freeze();
            return brush;
        }

        private static Color ToColor(Vector4 color)
            => Color.FromArgb(
                (byte)(color.W * 255),
                (byte)(color.X * 255),
                (byte)(color.Y * 255),
                (byte)(color.Z * 255));
    }
}
