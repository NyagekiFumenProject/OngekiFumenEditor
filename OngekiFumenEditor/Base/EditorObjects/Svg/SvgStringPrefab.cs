using System;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace OngekiFumenEditor.Base.EditorObjects.Svg
{
	public class SvgStringPrefab : SvgPrefabBase
	{
		public enum FlowDirection
		{
			LeftToRight,
			RightToLeft,
			TopToBottom,
			BottomToTop
		}

		public const string CommandName = "[SVG_STR]";
		public override string IDShortName => CommandName;

		private string content;
		public string Content
		{
			get => content;
			set => Set(ref content, value);
		}

		private FlowDirection contentFlowDirection = FlowDirection.LeftToRight;
		public FlowDirection ContentFlowDirection
		{
			get => contentFlowDirection;
			set => Set(ref contentFlowDirection, value);
		}

		private double fontSize = 16;
		public double FontSize
		{
			get => fontSize;
			set => Set(ref fontSize, value);
		}

		private double contentLineHeight = 16;
		public double ContentLineHeight
		{
			get => contentLineHeight;
			set => Set(ref contentLineHeight, value);
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
				case nameof(ColorfulLaneColor):
				case nameof(ContentLineHeight):
				case nameof(ContentFlowDirection):
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

			var brush = new SolidColorBrush(ColorfulLaneColor.Color);
			brush.Freeze();
			var pen = new Pen(brush, 1);
			pen.Freeze();
			var dpiInfo = VisualTreeHelper.GetDpi(Application.Current.MainWindow);

			var direction = ContentFlowDirection switch
			{
				FlowDirection.RightToLeft => System.Windows.FlowDirection.RightToLeft,
				_ => System.Windows.FlowDirection.LeftToRight
			};

			var content = Content;
			switch (ContentFlowDirection)
			{
				case FlowDirection.RightToLeft:
					content = new string(content.Reverse().ToArray());
					break;
				case FlowDirection.TopToBottom:
					content = string.Join(Environment.NewLine, content.Select(x => x));
					break;
				case FlowDirection.BottomToTop:
					content = string.Join(Environment.NewLine, content.Reverse());
					break;
				default:
					break;
			}

			var text = new FormattedText(
				content,
				CultureInfo.CurrentCulture,
				direction,
				new Typeface(TypefaceName),
				FontSize,
				brush,
				dpiInfo.PixelsPerDip
			);
			text.LineHeight = ContentLineHeight;

			Geometry geometry = text.BuildGeometry(new Point(0, 0));

			var group = new DrawingGroup();
			group.Children.Add(new GeometryDrawing() { Geometry = geometry, Brush = brush, Pen = pen });

			ApplySvgContent(group);
		}

		public override void Copy(OngekiObjectBase fromObj)
		{
			base.Copy(fromObj);

			if (fromObj is not SvgStringPrefab from)
				return;

			Content = from.Content;
			TypefaceName = from.TypefaceName;
			ContentLineHeight = from.ContentLineHeight;
			FontSize = from.FontSize;
			ContentFlowDirection = from.ContentFlowDirection;
		}
	}
}
