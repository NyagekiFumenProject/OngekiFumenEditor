using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.EditorSvgObjectControlProvider.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace OngekiFumenEditor.Base.EditorObjects.Svg
{
    public class SvgStringPrefab : SvgPrefabBase
    {
        public override Type ModelViewType => typeof(SvgStringPrefabViewModel);
        public override string IDShortName => $"SVG_STR";

        private string content;
        public string Content
        {
            get => content;
            set => Set(ref content, value);
        }

        private double fontSize = 16;
        public double FontSize
        {
            get => fontSize;
            set => Set(ref fontSize, value);
        }

        private ColorId fontColor = ColorIdConst.LaneGreen;
        public ColorId FontColor
        {
            get => fontColor;
            set => Set(ref fontColor, value);
        }

        private string typefaceName = "Tahoma";
        public string TypefaceName
        {
            get => typefaceName;
            set => Set(ref typefaceName, value);
        }

        public override void NotifyOfPropertyChange([CallerMemberName] string propertyName = null)
        {
            switch (propertyName)
            {
                case nameof(Content):
                case nameof(FontSize):
                case nameof(FontColor):
                case nameof(TypefaceName):
                    RebuildSvgContent();
                    break;
                default:
                    base.NotifyOfPropertyChange(propertyName);
                    break;
            }
        }

        public void RebuildSvgContent()
        {
            CleanGeometry();

            if (string.IsNullOrWhiteSpace(Content))
                return;

            var brush = new SolidColorBrush(FontColor.Color);
            brush.Freeze();
            var pen = new Pen(brush, 1);
            pen.Freeze();
            var dpiInfo = VisualTreeHelper.GetDpi(Application.Current.MainWindow);

            var text = new FormattedText(
                Content,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(TypefaceName),
                FontSize,
                brush,
                dpiInfo.PixelsPerDip
            );
            Geometry geometry = text.BuildGeometry(new Point(0, 0));

            var group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing() { Geometry = geometry, Brush = brush, Pen = pen });

            ApplySvgContent(group);
        }
    }
}
