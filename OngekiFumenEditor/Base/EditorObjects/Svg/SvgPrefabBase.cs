using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.CurveInterpolater;
using OngekiFumenEditor.Kernel.CurveInterpolater.DefaultImpl.Factory;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace OngekiFumenEditor.Base.EditorObjects.Svg
{
	public abstract class SvgPrefabBase : OngekiMovableObjectBase
	{
		private ICurveInterpolaterFactory curveInterpolaterFactory = XGridLimitedCurveInterpolaterFactory.Default;
		public ICurveInterpolaterFactory CurveInterpolaterFactory
		{
			get => curveInterpolaterFactory;
			set => Set(ref curveInterpolaterFactory, value);
		}

		private bool isForceColorful = false;
		public bool IsForceColorful
		{
			get => isForceColorful;
			set => Set(ref isForceColorful, value);
		}

		private ColorId colorfulLaneColor = ColorIdConst.LaneGreen;
		public ColorId ColorfulLaneColor
		{
			get => colorfulLaneColor;
			set => Set(ref colorfulLaneColor, value);
		}

		private RangeValue colorfulLaneBrightness = RangeValue.Create(-3, 3, 0);
		[ObjectPropertyBrowserSingleSelectedOnly]
		public RangeValue ColorfulLaneBrightness
		{
			get => colorfulLaneBrightness;
			set
			{
				this.RegisterOrUnregisterPropertyChangeEvent(colorfulLaneBrightness, value);
				Set(ref colorfulLaneBrightness, value);
			}
		}

		private RangeValue rotation = RangeValue.Create(-180, 180f, 0f);
		[ObjectPropertyBrowserSingleSelectedOnly]
		public RangeValue Rotation
		{
			get => rotation;
			set
			{
				this.RegisterOrUnregisterPropertyChangeEvent(rotation, value);
				Set(ref rotation, value);
			}
		}

		private RangeValue offsetX = RangeValue.CreateNormalized(0.5f);
		[ObjectPropertyBrowserSingleSelectedOnly]
		public RangeValue OffsetX
		{
			get => offsetX;
			set
			{
				this.RegisterOrUnregisterPropertyChangeEvent(offsetX, value);
				Set(ref offsetX, value);
			}
		}

		private RangeValue colorSimilar = RangeValue.Create(1, 1000, 600);
		[ObjectPropertyBrowserSingleSelectedOnly]
		public RangeValue ColorSimilar
		{
			get => colorSimilar;
			set
			{
				this.RegisterOrUnregisterPropertyChangeEvent(colorSimilar, value);
				Set(ref colorSimilar, value);
			}
		}

		private RangeValue offsetY = RangeValue.CreateNormalized(0.5f);
		[ObjectPropertyBrowserSingleSelectedOnly]
		public RangeValue OffsetY
		{
			get => offsetY;
			set
			{
				this.RegisterOrUnregisterPropertyChangeEvent(offsetY, value);
				Set(ref offsetY, value);
			}
		}

		private bool enableColorfulLaneSimilar = true;
		public bool EnableColorfulLaneSimilar
		{
			get => enableColorfulLaneSimilar;
			set
			{
				Set(ref enableColorfulLaneSimilar, value);
			}
		}

		private bool showOriginColor = false;
		public bool ShowOriginColor
		{
			get => showOriginColor;
			set
			{
				Set(ref showOriginColor, value);
			}
		}

		private float scale = 1;
		public float Scale
		{
			get => scale;
			set => Set(ref scale, value);
		}

		private RangeValue opacity = RangeValue.CreateNormalized(1);
		[ObjectPropertyBrowserSingleSelectedOnly]
		public RangeValue Opacity
		{
			get => opacity;
			set
			{
				this.RegisterOrUnregisterPropertyChangeEvent(opacity, value);
				Set(ref opacity, value);
			}
		}

		private RangeValue tolerance = RangeValue.Create(0.001f, 20f, 20f);
		[ObjectPropertyBrowserSingleSelectedOnly]
		public RangeValue Tolerance
		{
			get => tolerance;
			set
			{
				this.RegisterOrUnregisterPropertyChangeEvent(tolerance, value);
				Set(ref tolerance, value);
			}
		}

		private DrawingGroup drawingGroup;

		private DrawingGroup processingDrawingGroup;
		public DrawingGroup ProcessingDrawingGroup
		{
			get => processingDrawingGroup;
			set => Set(ref processingDrawingGroup, value);
		}

		public SvgPrefabBase()
		{
			Tolerance = Tolerance;
			Opacity = Opacity;
			Rotation = Rotation;
			OffsetX = OffsetX;
			OffsetY = OffsetY;
			ColorSimilar = ColorSimilar;
			ColorfulLaneBrightness = ColorfulLaneBrightness;
		}

		public override void Copy(OngekiObjectBase fromObj)
		{
			base.Copy(fromObj);
			if (fromObj is not SvgPrefabBase from)
				return;

			Tolerance = from.Tolerance;
			Opacity = from.Opacity;
			Rotation = from.Rotation;
			OffsetX = from.OffsetX;
			OffsetY = from.OffsetY;
			ColorSimilar = from.ColorSimilar;
			ShowOriginColor = from.ShowOriginColor;
			IsForceColorful = from.IsForceColorful;
			CurveInterpolaterFactory = from.CurveInterpolaterFactory;
			ColorfulLaneColor = from.ColorfulLaneColor;
			EnableColorfulLaneSimilar = from.EnableColorfulLaneSimilar;
		}

		public override void NotifyOfPropertyChange([CallerMemberName] string propertyName = null)
		{
			base.NotifyOfPropertyChange(propertyName);

			switch (propertyName)
			{
				case nameof(EnableColorfulLaneSimilar):
				case nameof(Rotation):
				case nameof(Scale):
				case nameof(ShowOriginColor):
				case nameof(IsForceColorful):
				case nameof(colorfulLaneColor):
				case nameof(Opacity):
				case nameof(OffsetX):
				case nameof(OffsetY):
				case nameof(ColorSimilar):
				case nameof(RangeValue.CurrentValue):
				case nameof(Tolerance):
					RebuildGeometry();
					break;
				default:
					break;
			}
		}

		protected void ApplySvgContent(DrawingGroup svgContent)
		{
			CleanGeometry();
			drawingGroup = svgContent;
			RebuildGeometry();
		}

		public void CleanGeometry()
		{
			drawingGroup = null;
			ProcessingDrawingGroup = default;
		}

		public void RebuildGeometry()
		{
			ProcessingDrawingGroup = default;
			var inter = drawingGroup?.Children?.FirstOrDefault();
			if (inter is null)
				return;

			var procDrawingGroup = new DrawingGroup();
			var bound = drawingGroup.Bounds;

			var transform = new TransformGroup();
			transform.Children.Add(new TranslateTransform()
			{
				X = -OffsetX.CurrentValue * bound.Width,
				Y = -OffsetY.CurrentValue * bound.Height
			});
			transform.Children.Add(new ScaleTransform()
			{
				ScaleX = Scale,
				ScaleY = Scale,
			});
			transform.Children.Add(new RotateTransform()
			{
				Angle = Rotation.CurrentValue
			});

			Geometry GenFlattedGeometry(Geometry geometry)
			{
				var flattedGeometry = geometry.GetFlattenedPathGeometry(Tolerance.CurrentValue, ToleranceType.Absolute);
				flattedGeometry.Transform = transform;
				flattedGeometry.Freeze();
				return flattedGeometry;
			}

			void VisitGeometryDrawing(GeometryDrawing geometryDrawing, DrawingGroup parentGroup = default)
			{
				if (GenFlattedGeometry(geometryDrawing.Geometry) is not Geometry geometry)
					return;

				var newDrawing = new GeometryDrawing();
				newDrawing.Geometry = geometry;
				newDrawing.Pen = CalculateRelativePen(geometryDrawing.Pen) ?? CalculateRelativePen(newDrawing.Brush);
				newDrawing.Brush = default;
				newDrawing.Freeze();

				//append to list
				procDrawingGroup.Children.Add(newDrawing);
			}

			void VisitGroup(DrawingGroup group, DrawingGroup parentGroup = default)
			{
				foreach (var child in group.Children.OfType<DrawingGroup>())
					VisitGroup(child, group);
				foreach (var child in group.Children.OfType<GeometryDrawing>())
					VisitGeometryDrawing(child, group);
			}

			VisitGroup(drawingGroup);

			procDrawingGroup.Freeze();
			ProcessingDrawingGroup = procDrawingGroup;

			Log.LogDebug($"Generate {ProcessingDrawingGroup.Children.Count} geometries. hashCode:{ProcessingDrawingGroup?.GetHashCode()}");
		}

		private Pen CalculateRelativePen(Brush brush)
		{
			var pen = new Pen(brush, 2);
			pen.Freeze();
			return CalculateRelativePen(pen);
		}

		public LaneColor? PickSimilarLaneColor(Color color)
		{
			var arr = LaneColor.AllLaneColors.Where(x => x.LaneType switch
			{
				LaneType.WallRight or LaneType.WallLeft => false,
				_ => true
			});

			if (!EnableColorfulLaneSimilar)
				arr = arr.Where(x => x.LaneType != LaneType.Colorful);

			var r = arr
				.Select(x => (x, x.Color.ColorDistance(color)))
				.OrderBy(x => x.Item2);

			return r.Where(x => x.Item2 < ColorSimilar.CurrentValue).Select(x => x.x).FirstOrDefault();
		}

		private Pen CalculateRelativePen(Pen pen)
		{
			Color PickColor(Color color)
			{
				return PickSimilarLaneColor(color)?.Color ?? Colors.Transparent;
			}

			if (ShowOriginColor)
				return pen;

			var color = IsForceColorful ? ColorfulLaneColor.Color : (pen?.Brush is SolidColorBrush b ? PickColor(b.Color) : Colors.Green);
			var brush = new SolidColorBrush(Color.FromArgb((byte)(Opacity.CurrentValue * color.A), color.R, color.G, color.B));
			brush.Freeze();

			var p = new Pen(brush, 2);
			p.Freeze();

			return p;
		}

		public class LineSegment
		{
			public List<Vector2> RelativePoints { get; set; } = new List<Vector2>();
			public Color Color { get; set; }
		}

		public List<LineSegment> GenerateLineSegments()
		{
			var outputSegments = new List<LineSegment>();

			if (ProcessingDrawingGroup is not DrawingGroup drawingGroup)
				return outputSegments;

			var bound = drawingGroup.Bounds.Size;
			var offset = drawingGroup.Bounds.Location;

			foreach (var childGeometry in drawingGroup.Children.OfType<GeometryDrawing>())
			{
				var lines = childGeometry.Geometry.GetFlattenedPathGeometry();
				var brush = (childGeometry.Brush ?? childGeometry.Pen?.Brush) as SolidColorBrush;
				var color = brush?.Color ?? Colors.White;

				if (!ShowOriginColor)
				{
					if (PickSimilarLaneColor(brush.Color) is LaneColor laneColor)
						color = laneColor.Color;
					else
						continue;
				}

				Vector2 CalculateRelativePoint(Point relativePoint)
				{
					var rx = -(bound.Width - relativePoint.X) - offset.X + bound.Width * (1 - OffsetX.ValuePercent);
					var ry = -relativePoint.Y + offset.Y + bound.Height * OffsetY.ValuePercent;

					//Log.LogDebug($"{relativePoint}  ->  {new Vector2((float)rx, (float)ry)}");
					return new((float)rx, (float)ry);
				}

				foreach (var path in lines.Figures.OfType<PathFigure>())
				{
					var segment = new LineSegment();
					segment.Color = color;

					var points = path.Segments.SelectMany(x => x switch
					{
						System.Windows.Media.LineSegment ls => Enumerable.Repeat(ls.Point, 0),
						PolyLineSegment pls => pls.Points,
						_ => Enumerable.Empty<Point>()
					}).Prepend(path.StartPoint).ToList();

					var firstP = points[0];
					segment.RelativePoints.Add(CalculateRelativePoint(firstP));

					foreach (var childP in points.Skip(1).SkipLast(1))
					{
						segment.RelativePoints.Add(CalculateRelativePoint(childP));
					}

					var lastP = points.LastOrDefault();
					segment.RelativePoints.Add(CalculateRelativePoint(lastP));

					outputSegments.Add(segment);
				}
			}

			return outputSegments;
		}

		public override string ToString() => $"{base.ToString()} R[∠{Rotation}°] O[{Opacity.ValuePercent * 100:F2}%] S[{Rotation:F2}x]";
	}
}
