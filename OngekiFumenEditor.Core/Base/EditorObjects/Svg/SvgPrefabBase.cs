using OngekiFumenEditor.Core.Base.Attributes;
using OngekiFumenEditor.Core.Base.OngekiObjects;
using OngekiFumenEditor.Core.Base.ValueTypes;
using OngekiFumenEditor.Core.Kernel.CurveInterpolater;
using OngekiFumenEditor.Core.Kernel.CurveInterpolater.OgkrImpl.Factory;
using OngekiFumenEditor.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Core.Base.EditorObjects.Svg
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

		private ColorId colorfulLaneColor = ColorIdConst.Yuzu;
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
			set => Set(ref enableColorfulLaneSimilar, value);
		}

		private bool showOriginColor = false;
		public bool ShowOriginColor
		{
			get => showOriginColor;
			set => Set(ref showOriginColor, value);
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

		private VectorScene vectorScene;

		private VectorScene processingVectorScene;
		public VectorScene ProcessingVectorScene
		{
			get => processingVectorScene;
			set => Set(ref processingVectorScene, value);
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

		public void ApplySvgContent(VectorScene svgContent)
		{
			CleanGeometry();
			vectorScene = svgContent;
			RebuildGeometry();
		}

		public void CleanGeometry()
		{
			vectorScene = null;
			ProcessingVectorScene = default;
		}

		public void RebuildGeometry()
		{
			ProcessingVectorScene = default;
			if (vectorScene is null || vectorScene.Paths.Count == 0)
				return;

			var sourceBounds = vectorScene.Bounds;
			var centerX = OffsetX.CurrentValue * sourceBounds.Width;
			var centerY = OffsetY.CurrentValue * sourceBounds.Height;
			var radians = Math.PI * Rotation.CurrentValue / 180.0;
			var cos = Math.Cos(radians);
			var sin = Math.Sin(radians);

			VectorPoint TransformPoint(VectorPoint point)
			{
				var x = point.X - centerX;
				var y = point.Y - centerY;

				x *= Scale;
				y *= Scale;

				var rx = x * cos - y * sin;
				var ry = x * sin + y * cos;

				return new VectorPoint(rx, ry);
			}

			Color ResolveColor(Color sourceColor)
			{
				if (ShowOriginColor)
					return Color.FromArgb((byte)(Opacity.CurrentValue * sourceColor.A), sourceColor.R, sourceColor.G, sourceColor.B);

				var pickedColor = IsForceColorful
					? ColorfulLaneColor.Color
					: PickSimilarLaneColor(sourceColor)?.Color ?? Colors.Green;

				return Color.FromArgb((byte)(Opacity.CurrentValue * pickedColor.A), pickedColor.R, pickedColor.G, pickedColor.B);
			}

			var output = new VectorScene
			{
				Bounds = vectorScene.Bounds,
				Paths = vectorScene.Paths
					.Where(x => x.Points.Count > 0)
					.Select(path => new VectorPath
					{
						IsClosed = path.IsClosed,
						Color = ResolveColor(path.Color),
						Points = path.Points.Select(TransformPoint).ToList()
					})
					.ToList()
			};

			ProcessingVectorScene = output;
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
				.Select(x => (x, CalculateColorDistance(x.Color, color)))
				.OrderBy(x => x.Item2);

			return r.Where(x => x.Item2 < ColorSimilar.CurrentValue).Select(x => x.x).FirstOrDefault();
		}

		public class LineSegment
		{
			public List<Vector2> RelativePoints { get; set; } = new();
			public Color Color { get; set; }
		}

		public List<LineSegment> GenerateLineSegments()
		{
			var outputSegments = new List<LineSegment>();

			if (ProcessingVectorScene is not VectorScene vectorScene)
				return outputSegments;

			var bound = vectorScene.Bounds;

			Vector2 CalculateRelativePoint(VectorPoint relativePoint)
			{
				var rx = -(bound.Width - relativePoint.X) - bound.X + bound.Width * (1 - OffsetX.ValuePercent);
				var ry = -relativePoint.Y + bound.Y + bound.Height * OffsetY.ValuePercent;
				return new((float)rx, (float)ry);
			}

			foreach (var path in vectorScene.Paths.Where(x => x.Points.Count > 0))
			{
				var segment = new LineSegment
				{
					Color = path.Color
				};

				foreach (var point in path.Points)
					segment.RelativePoints.Add(CalculateRelativePoint(point));

				outputSegments.Add(segment);
			}

			return outputSegments;
		}

		public override string ToString() => $"{base.ToString()} R[{Rotation:F2}] O[{Opacity.ValuePercent * 100:F2}%] S[{Scale:F2}x]";

		private static double CalculateColorDistance(Color a, Color b)
		{
			var dr = a.R - b.R;
			var dg = a.G - b.G;
			var db = a.B - b.B;
			var da = a.A - b.A;
			return Math.Sqrt(dr * dr + dg * dg + db * db + da * da);
		}

		public override void Dispose()
		{
			base.Dispose();

			ColorfulLaneBrightness = default;
			Rotation = default;
			OffsetX = default;
			OffsetY = default;
			Opacity = default;
			Tolerance = default;
			ColorSimilar = default;
		}
	}
}

