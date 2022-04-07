using OngekiFumenEditor.Modules.EditorSvgObjectControlProvider.ViewModels;
using OngekiFumenEditor.Utils;
using SharpVectors.Renderers.Wpf;
using SvgConverter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OngekiFumenEditor.Base.EditorObjects.Svg
{
    public class SvgPrefab : OngekiMovableObjectBase
    {
        public override string IDShortName => "SVG";
        public override Type ModelViewType => typeof(SvgPrefabViewModel);

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

        private FileInfo svgFile = null;
        private DrawingGroup drawingGroup;

        private DrawingGroup processingDrawingGroup;
        public DrawingGroup ProcessingDrawingGroup
        {
            get => processingDrawingGroup;
            set => Set(ref processingDrawingGroup, value);
        }

        public FileInfo SvgFile
        {
            get => svgFile;
            set => Set(ref svgFile, value);
        }

        public SvgPrefab()
        {
            Tolerance = Tolerance;
            Opacity = Opacity;
            Rotation = Rotation;
        }

        public override void NotifyOfPropertyChange([CallerMemberName] string propertyName = null)
        {
            base.NotifyOfPropertyChange(propertyName);

            switch (propertyName)
            {
                case nameof(SvgFile):
                    ReloadSvgFile();
                    break;
                case nameof(Rotation):
                case nameof(Scale):
                case nameof(Opacity):
                case nameof(RangeValue.CurrentValue):
                case nameof(Tolerance):
                    RebuildGeometry();
                    break;
                default:
                    break;
            }
        }

        private void ReloadSvgFile()
        {
            CleanGeometry();

            if (SvgFile is null)
                return;

            drawingGroup = ConverterLogic.ConvertSvgToObject(SvgFile.FullName, ResultMode.DrawingGroup, new WpfDrawingSettings()
            {
                IncludeRuntime = false,
                TextAsGeometry = true,
                OptimizePath = true,
                EnsureViewboxSize = true
            }, out _, new()) as DrawingGroup;
            drawingGroup.Freeze();

            RebuildGeometry();
        }

        private void CleanGeometry()
        {
            drawingGroup = null;
            ProcessingDrawingGroup = default;
        }

        private void RebuildGeometry()
        {
            ProcessingDrawingGroup = default;
            var inter = drawingGroup.Children.FirstOrDefault();
            if (inter is null)
                return;

            var procDrawingGroup = new DrawingGroup();
            var transform = new TransformGroup();
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
                /*
                if (geometry is RectangleGeometry)
                    return default;
                */
                /*
                var r = geometry.GetFlattenedPathGeometry();
                var flattedGeometry = new PathGeometry();
                var fig = new PathFigure();
                r.GetPointAtFractionLength(0, out var point, out _);
                fig.StartPoint = point;
                for (var i = RefSvgPrefab.Tolerance; i < 1; i += RefSvgPrefab.Tolerance)
                {
                    r.GetPointAtFractionLength(i, out point, out _);
                    fig.Segments.Add(new LineSegment()
                    {
                        IsStroked = true,
                        Point = point,
                    });
                }
                r.GetPointAtFractionLength(1, out point, out _);
                fig.Segments.Add(new LineSegment()
                {
                    IsStroked = true,
                    Point = point,
                });
                flattedGeometry.Figures.Add(fig);
                /**/
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
                newDrawing.Brush = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
                newDrawing.Pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(Opacity.CurrentValue * 255), 0, 0, 255)), 2);
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

            Log.LogDebug($"Generate {ProcessingDrawingGroup.Children.Count} geometries from svg file: {SvgFile}.");
        }

        public override string ToString() => $"{base.ToString()} R:∠{Rotation}° O:{Opacity.ValuePercent * 100:F2}% S:{Rotation:F2}x File:{Path.GetFileName(SvgFile?.Name)}";
    }
}
