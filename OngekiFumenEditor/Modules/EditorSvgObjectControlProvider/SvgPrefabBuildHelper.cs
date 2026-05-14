using OngekiFumenEditor.Core.Base.EditorObjects.Svg;
using OngekiFumenEditor.Core.Base.ValueTypes;
using SharpVectors.Renderers.Wpf;
using SvgConverter;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CoreColor = OngekiFumenEditor.Core.Base.ValueTypes.Color;
using CoreColors = OngekiFumenEditor.Core.Base.ValueTypes.Colors;
using WpfColor = System.Windows.Media.Color;

namespace OngekiFumenEditor.Modules.EditorSvgObjectControlProvider
{
	internal static class SvgPrefabBuildHelper
	{
		public static bool EnsureBuilt(SvgPrefabBase prefab)
		{
			if (prefab?.ProcessingVectorScene is not null)
				return true;

			var scene = prefab switch
			{
				SvgImageFilePrefab image => Build(image),
				SvgStringPrefab text => Build(text),
				_ => null
			};

			if (scene is null)
				return false;

			prefab.ApplySvgContent(scene);
			return true;
		}

		private static VectorScene Build(SvgImageFilePrefab prefab)
		{
			if (prefab.SvgFile is null || !prefab.SvgFile.Exists)
				return null;

			var svgContent = ConverterLogic.ConvertSvgToObject(prefab.SvgFile.FullName, ResultMode.DrawingGroup, new WpfDrawingSettings()
			{
				IncludeRuntime = false,
				TextAsGeometry = true,
				OptimizePath = true,
				EnsureViewboxSize = true
			}, out _, new()) as DrawingGroup;

			if (svgContent is null)
				return null;

			svgContent.Freeze();

			var bounds = svgContent.Bounds;
			var scene = new VectorScene
			{
				Bounds = new VectorRect(bounds.X, bounds.Y, bounds.Width, bounds.Height)
			};

			void VisitGroup(DrawingGroup group)
			{
				foreach (var child in group.Children.OfType<DrawingGroup>())
					VisitGroup(child);

				foreach (var child in group.Children.OfType<GeometryDrawing>())
				{
					var brush = (child.Brush ?? child.Pen?.Brush) as SolidColorBrush;
					var color = brush is not null ? FromWpfColor(brush.Color) : CoreColors.White;
					var lines = child.Geometry.GetFlattenedPathGeometry();

					foreach (var figure in lines.Figures.OfType<PathFigure>())
					{
						var path = new VectorPath
						{
							Color = color,
							IsClosed = figure.IsClosed
						};

						var points = figure.Segments.SelectMany(x => x switch
						{
							System.Windows.Media.LineSegment ls => Enumerable.Repeat(ls.Point, 1),
							PolyLineSegment pls => pls.Points,
							_ => Enumerable.Empty<Point>()
						}).Prepend(figure.StartPoint);

						path.Points.AddRange(points.Select(x => new VectorPoint(x.X, x.Y)));

						if (path.Points.Count > 0)
							scene.Paths.Add(path);
					}
				}
			}

			VisitGroup(svgContent);
			return scene;
		}

		private static VectorScene Build(SvgStringPrefab prefab)
		{
			if (string.IsNullOrWhiteSpace(prefab.Content))
				return null;

			var brush = new SolidColorBrush(ToWpfColor(prefab.ColorfulLaneColor.Color));
			brush.Freeze();
			var dpiInfo = Application.Current.MainWindow is not null ? VisualTreeHelper.GetDpi(Application.Current.MainWindow) : new();

			var direction = prefab.ContentFlowDirection switch
			{
				SvgStringPrefab.FlowDirection.RightToLeft => System.Windows.FlowDirection.RightToLeft,
				_ => System.Windows.FlowDirection.LeftToRight
			};

			var content = prefab.Content;
			switch (prefab.ContentFlowDirection)
			{
				case SvgStringPrefab.FlowDirection.RightToLeft:
					content = new string(content.Reverse().ToArray());
					break;
				case SvgStringPrefab.FlowDirection.TopToBottom:
					content = string.Join(Environment.NewLine, content.Select(x => x));
					break;
				case SvgStringPrefab.FlowDirection.BottomToTop:
					content = string.Join(Environment.NewLine, content.Reverse());
					break;
			}

			var text = new FormattedText(
				content,
				System.Globalization.CultureInfo.CurrentCulture,
				direction,
				new Typeface(prefab.TypefaceName),
				prefab.FontSize,
				brush,
				dpiInfo.PixelsPerDip
			);
			text.LineHeight = prefab.ContentLineHeight;

			var geometry = text.BuildGeometry(new Point(0, 0)).GetFlattenedPathGeometry();
			var bounds = geometry.Bounds;
			var scene = new VectorScene
			{
				Bounds = new VectorRect(bounds.X, bounds.Y, bounds.Width, bounds.Height)
			};

			foreach (var figure in geometry.Figures.OfType<PathFigure>())
			{
				var path = new VectorPath
				{
					Color = prefab.ColorfulLaneColor.Color,
					IsClosed = figure.IsClosed
				};

				var points = figure.Segments.SelectMany(x => x switch
				{
					System.Windows.Media.LineSegment ls => Enumerable.Repeat(ls.Point, 1),
					PolyLineSegment pls => pls.Points,
					_ => Enumerable.Empty<Point>()
				}).Prepend(figure.StartPoint);

				path.Points.AddRange(points.Select(x => new VectorPoint(x.X, x.Y)));

				if (path.Points.Count > 0)
					scene.Paths.Add(path);
			}

			return scene;
		}

		private static CoreColor FromWpfColor(WpfColor color) => CoreColor.FromArgb(color.A, color.R, color.G, color.B);

		private static WpfColor ToWpfColor(CoreColor color) => WpfColor.FromArgb(color.A, color.R, color.G, color.B);
	}
}
