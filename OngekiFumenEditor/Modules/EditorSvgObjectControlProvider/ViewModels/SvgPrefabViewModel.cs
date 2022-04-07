using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base.EditorObjects.Svg;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using SharpVectors.Renderers.Wpf;
using SvgConverter;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.EditorSvgObjectControlProvider.ViewModels
{
    [Gemini.Modules.Toolbox.ToolboxItem(typeof(FumenVisualEditorViewModel), "SvgPrefab", "Misc")]
    public class SvgPrefabViewModel : DisplayObjectViewModelBase<SvgPrefab>
    {
        public SvgPrefab RefSvgPrefab => (SvgPrefab)ReferenceOngekiObject;

        protected DrawingGroup drawingGroup = default;

        public Rect GeometryBound => ProcessingDrawingGroup?.Bounds ?? Rect.Empty;

        private DrawingGroup processedDrawingGroup = default;
        public DrawingGroup ProcessingDrawingGroup
        {
            get => processedDrawingGroup;
            set
            {
                Set(ref processedDrawingGroup, value);
                NotifyOfPropertyChange(() => GeometryBound);
            }
        }

        public override void OnObjectCreated(object createFrom, FumenVisualEditorViewModel editorViewModel)
        {
            base.OnObjectCreated(createFrom, editorViewModel);
            ReloadSvgFile();
        }

        protected override void OnOngekiObjectPropChanged(object sender, PropertyChangedEventArgs arg)
        {
            switch (arg.PropertyName)
            {
                case nameof(SvgPrefab.SvgFilePath):
                    ReloadSvgFile();
                    break;
                case nameof(SvgPrefab.Rotation):
                case nameof(SvgPrefab.Scale):
                case nameof(SvgPrefab.Opacity):
                case nameof(SvgPrefab.Tolerance):
                    RebuildGeometry();
                    break;
                default:
                    base.OnOngekiObjectPropChanged(sender, arg);
                    break;
            }
        }

        private void ReloadSvgFile()
        {
            CleanGeometry();

            if (!File.Exists(RefSvgPrefab.SvgFilePath))
                return;

            drawingGroup = ConverterLogic.ConvertSvgToObject(RefSvgPrefab.SvgFilePath, ResultMode.DrawingGroup, new WpfDrawingSettings()
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
                ScaleX = RefSvgPrefab.Scale,
                ScaleY = RefSvgPrefab.Scale,
            });
            transform.Children.Add(new RotateTransform()
            {
                Angle = RefSvgPrefab.Rotation
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
                var flattedGeometry = geometry.GetFlattenedPathGeometry(RefSvgPrefab.Tolerance, ToleranceType.Absolute);
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
                newDrawing.Pen = new Pen(new SolidColorBrush(Color.FromArgb(255, 0, 0, 255)), 2);
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

            Log.LogDebug($"Generate {ProcessingDrawingGroup.Children} geometries from svg file: {RefSvgPrefab.SvgFilePath}.");
        }
    }
}
