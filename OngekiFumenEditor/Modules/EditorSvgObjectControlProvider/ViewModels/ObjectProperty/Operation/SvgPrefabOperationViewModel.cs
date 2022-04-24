using Caliburn.Micro;
using OngekiFumenEditor.Base.EditorObjects.Svg;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.EditorSvgObjectControlProvider.ViewModels.ObjectProperty.Operation
{
    public class SvgPrefabOperationViewModel : PropertyChangedBase
    {
        public SvgPrefab SvgPrefab { get; }

        public SvgPrefabOperationViewModel(SvgPrefab svgPrefab)
        {
            SvgPrefab = svgPrefab;
        }

        public void OnGenerateLaneToEditor()
        {
            if (IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor is not FumenVisualEditorViewModel editor)
            {
                MessageBox.Show("请先切换到当前编辑器");
                return;
            }

            if (SvgPrefab.ProcessingDrawingGroup is not DrawingGroup drawingGroup)
            {
                MessageBox.Show("无效的SVG内容");
                return;
            }

            if (SvgPrefab.ShowOriginColor)
            {
                MessageBox.Show("请先取消勾选ShowOriginColor进行将要输出轨道的预览");
                return;
            }

            var baseCanvasX = XGridCalculator.ConvertXGridToX(SvgPrefab.XGrid, editor);
            var baseCanvasY = TGridCalculator.ConvertTGridToY(SvgPrefab.TGrid, editor);
            var bound = drawingGroup.Bounds.Size;
            var offset = drawingGroup.Bounds.Location;

            Log.LogDebug($"({baseCanvasX:F2},{baseCanvasY:F2})");

            foreach (var childGeometry in drawingGroup.Children.OfType<GeometryDrawing>())
            {
                var lines = childGeometry.Geometry.GetFlattenedPathGeometry();
                var brush = childGeometry.Brush ?? childGeometry.Pen?.Brush;

                LaneStartBase targetObject = GetLaneTypeFromBrush(brush) switch
                {
                    LaneType.Left => new LaneLeftStart(),
                    LaneType.Center => new LaneCenterStart(),
                    LaneType.Right => new LaneRightStart(),
                    LaneType.Colorful => new ColorfulLaneStart(),
                    _ => null
                };

                void CommomBuildUp(Point relativePoint, ConnectableObjectBase obj)
                {
                    var actualCanvasX = baseCanvasX - (bound.Width - relativePoint.X) - offset.X + bound.Width * (1 - SvgPrefab.OffsetX.ValuePercent);
                    var actualCanvasY = baseCanvasY - relativePoint.Y + offset.Y + bound.Height * SvgPrefab.OffsetY.ValuePercent;

                    var tGrid = TGridCalculator.ConvertYToTGrid(actualCanvasY, editor);
                    var xGrid = XGridCalculator.ConvertXToXGrid(actualCanvasX, editor);

                    obj.XGrid = xGrid;
                    obj.TGrid = tGrid;
                }

                foreach (var path in lines.Figures.OfType<PathFigure>())
                {
                    var points = path.Segments.SelectMany(x => x switch
                    {
                        LineSegment ls => Enumerable.Repeat(ls.Point, 0),
                        PolyLineSegment pls => pls.Points,
                        _ => Enumerable.Empty<Point>()
                    }).Prepend(path.StartPoint).ToArray();

                    var firstP = points[0];
                    var startObj = (LambdaActivator.CreateInstance(targetObject.ModelViewType) as DisplayObjectViewModelBase).ReferenceOngekiObject as ConnectableStartObject;
                    CommomBuildUp(firstP, startObj);

                    foreach (var childP in points.Skip(1).SkipLast(1))
                    {
                        var nextObj = LambdaActivator.CreateInstance(targetObject.NextType) as ConnectableChildObjectBase;
                        CommomBuildUp(childP, nextObj);
                        startObj.AddChildObject(nextObj);

                    }

                    var lastP = points.LastOrDefault();
                    var endObj = LambdaActivator.CreateInstance(targetObject.EndType) as ConnectableChildObjectBase;
                    CommomBuildUp(lastP, endObj);
                    startObj.AddChildObject(endObj);

                    editor.Fumen.AddObject(startObj);
                }
            }
        }

        private LaneType GetLaneTypeFromBrush(Brush brush)
        {
            if (brush is not SolidColorBrush colorBrush)
                return LaneType.Colorful;

            var color = colorBrush.Color;
            color.A = 255;

            return LaneColor.AllLaneColors
                 .Select(x => (x.LaneType, x.Color.ColorDistance(color)))
                 .OrderBy(x => x.Item2).FirstOrDefault().LaneType;
        }
    }
}
