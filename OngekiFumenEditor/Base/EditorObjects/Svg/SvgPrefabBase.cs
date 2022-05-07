using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.CurveInterpolater;
using OngekiFumenEditor.Kernel.CurveInterpolater.DefaultImpl.Factory;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OngekiFumenEditor.Base.EditorObjects.Svg
{
    public abstract class SvgPrefabBase : OngekiMovableObjectBase
    {
        private ICurveInterpolaterFactory curveInterpolaterFactory = DefaultCurveInterpolaterFactory.Default;
        public ICurveInterpolaterFactory CurveInterpolaterFactory
        {
            get => curveInterpolaterFactory;
            set => Set(ref curveInterpolaterFactory, value);
        }

        private RangeValue rotation = RangeValue.Create(0, 360f, 0f);
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

        private bool showOriginColor = true;
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
        public RangeValue Opacity
        {
            get => opacity;
            set
            {
                this.RegisterOrUnregisterPropertyChangeEvent(opacity, value);
                Set(ref opacity, value);
            }
        }

        private RangeValue tolerance = RangeValue.Create(0, 20f, 1f);
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

        private void CleanGeometry()
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

            Log.LogDebug($"Generate {ProcessingDrawingGroup.Children.Count} geometries.");
        }

        private Pen CalculateRelativePen(Brush brush)
        {
            var pen = new Pen(brush, 2);
            pen.Freeze();
            return CalculateRelativePen(pen);
        }

        private Pen CalculateRelativePen(Pen pen)
        {
            Color PickColor(Color color)
            {
                var arr = LaneColor.AllLaneColors;
                if (!EnableColorfulLaneSimilar)
                    arr = arr.Where(x => x.LaneType != LaneType.Colorful);

                var r = arr
                    .Select(x => (x.Color, x.Color.ColorDistance(color)))
                    .OrderByDescending(x => x.Item2)
                    .Where(x => x.Item2 > ColorSimilar.CurrentValue);

                return r.Select(x => x.Color).FirstOrDefault();
            }
            if (ShowOriginColor)
                return pen;
            var color = pen?.Brush is SolidColorBrush b ? PickColor(b.Color) : Colors.Green;
            var brush = new SolidColorBrush(Color.FromArgb((byte)(Opacity.CurrentValue * color.A), color.R, color.G, color.B));
            brush.Freeze();
            var p = new Pen(brush, 2);
            p.Freeze();
            return p;
        }

        public override string ToString() => $"{base.ToString()} R:∠{Rotation}° O:{Opacity.ValuePercent * 100:F2}% S:{Rotation:F2}x";
    }
}
