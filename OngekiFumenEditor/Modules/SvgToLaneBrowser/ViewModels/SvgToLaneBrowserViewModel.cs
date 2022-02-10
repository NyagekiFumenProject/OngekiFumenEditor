using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using Microsoft.Win32;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Modules.SvgToLaneBrowser.Views;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using SimpleSvg2LineSegementInterpolater;
using SimpleSvg2LineSegementInterpolater.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.SvgToLaneBrowser.ViewModels
{
    [Export(typeof(ISvgToLaneBrowser))]
    public class SvgToLaneBrowserViewModel : WindowBase, ISvgToLaneBrowser
    {
        private InterpolaterOption interpolaterOption = new InterpolaterOption();

        private Brush previewBackground = Brushes.Gray;
        public Brush PreviewBackground
        {
            get => previewBackground;
            set => Set(ref previewBackground, value);
        }

        public Brush[] PreviewBackgroundValues { get; } = new[]
        {
            Brushes.Gray,
            Brushes.GreenYellow,
            Brushes.LightPink,
            Brushes.Orange,
            Brushes.LightYellow,
            Brushes.Black,
            Brushes.White,
        };

        private TGrid tGrid = new TGrid();
        public TGrid TGrid
        {
            get => tGrid;
            set
            {
                this.RegisterOrUnregisterPropertyChangeEvent(TGrid, value);
                Set(ref tGrid, value);
            }
        }

        private XGrid xGrid = new XGrid();
        public XGrid XGrid
        {
            get => xGrid;
            set
            {
                this.RegisterOrUnregisterPropertyChangeEvent(XGrid, value);
                Set(ref xGrid, value);
            }
        }

        private string svgFilePath;
        public string SvgFilePath
        {
            get => svgFilePath;
            set
            {
                Set(ref svgFilePath, value);
                RegenerateSVGContent();
            }
        }

        private FumenVisualEditorViewModel editor;
        public FumenVisualEditorViewModel Editor
        {
            get => editor;
            set
            {
                Set(ref editor, value);
                NotifyOfPropertyChange(() => IsEnableOutput);
            }
        }

        public bool IsEnableOutput => Editor is not null;

        private bool isShowOutputableOnly;
        public bool IsShowOutputableOnly
        {
            get => isShowOutputableOnly;
            set
            {
                Set(ref isShowOutputableOnly, value);
            }
        }

        public bool IsCircleSimplyAsLessPolygon
        {
            get => interpolaterOption.CircleSimplyAsLessPolygon;
            set
            {
                interpolaterOption.CircleSimplyAsLessPolygon = value;
                NotifyOfPropertyChange(() => IsCircleSimplyAsLessPolygon);
            }
        }

        private bool isShowLaneColor;
        public bool IsShowLaneColor
        {
            get => isShowLaneColor;
            set
            {
                Set(ref isShowLaneColor, value);
            }
        }

        public float Scale
        {
            get => interpolaterOption.Scale;
            set
            {
                interpolaterOption.Scale = value;
                NotifyOfPropertyChange(() => Scale);
            }
        }

        private float colorSimilar = 0;
        public float SolorSimilar
        {
            get => colorSimilar;
            set
            {
                Set(ref colorSimilar, value);
                UpdateLinesLaneTarget();
            }
        }

        private float edgeSimplfy = 0.1f;
        public float EdgeSimplfy
        {
            get => edgeSimplfy;
            set
            {
                Set(ref edgeSimplfy, value);
                UpdateLinesLaneTarget();
            }
        }

        private float edgeCloseSimplfy = 0.1f;
        public float EdgeCloseSimplfy
        {
            get => edgeCloseSimplfy;
            set
            {
                Set(ref edgeCloseSimplfy, value);
                UpdateLinesLaneTarget();
            }
        }

        private PointVerticalAlignTarget pointVerticalAlignTarget;
        public PointVerticalAlignTarget PointVerticalAlignTarget
        {
            get => pointVerticalAlignTarget;
            set
            {
                Set(ref pointVerticalAlignTarget, value);
                UpdateHorizonVerticalAlignPreview();
            }
        }

        public IEnumerable<PointVerticalAlignTarget> PointVerticalAlignTargetValues => Enum.GetValues<PointVerticalAlignTarget>();

        private PointHorizonAlignTarget pointHorizonAlignTarget;
        public PointHorizonAlignTarget PointHorizonAlignTarget
        {
            get => pointHorizonAlignTarget;
            set
            {
                Set(ref pointHorizonAlignTarget, value);
                UpdateHorizonVerticalAlignPreview();
            }
        }

        public IEnumerable<PointHorizonAlignTarget> PointHorizonAlignTargetValues => Enum.GetValues<PointHorizonAlignTarget>();

        private int beforeOptimzePointsCount;
        public int BeforeOptimzePointsCount
        {
            get => beforeOptimzePointsCount;
            set
            {
                Set(ref beforeOptimzePointsCount, value);
            }
        }

        public int AfterOptimzePointsCount => LineSegments.Select(x => x.RawLineSegmentCollection.Points.Count).Sum();
        public int OutputOptimzePointsCount => LineSegments.Where(x => x.LaneTarget != null).Select(x => x.RawLineSegmentCollection.Points.Count).Sum();

        public ObservableCollection<LineSegmentWrapper> LineSegments { get; } = new ObservableCollection<LineSegmentWrapper>();

        private Point canvasTranslateOffset;

        public Rect CurrentSegmentsBound { get; private set; }

        public Point CanvasTranslateOffset
        {
            get => canvasTranslateOffset;
            set
            {
                Set(ref canvasTranslateOffset, value);
            }
        }

        private Point currentOriginOffset;
        public Point CurrentOriginOffset
        {
            get => currentOriginOffset;
            set
            {
                Set(ref currentOriginOffset, value);
            }
        }

        public SvgToLaneBrowserViewModel()
        {
            IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += (n, o) => Editor = n;
            Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
        }

        public void OnLoadSvgFile()
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = FileDialogFilterHelper.BuildExtensionFilter((".svg", "Scalable Vector Graphics File Format"));
            dialog.Multiselect = false;
            dialog.CheckFileExists = true;
            if (dialog.ShowDialog() == true)
            {
                var filePath = dialog.FileName;
                SvgFilePath = filePath;
            }
        }

        public async void RegenerateSVGContent()
        {
            var lineSegements = (await Interpolater.GenerateInterpolatedLineSegmentAsync(await File.ReadAllTextAsync(SvgFilePath), interpolaterOption)).Where(x => x.Points.Count > 0).ToList();
            BeforeOptimzePointsCount = lineSegements.Select(x => x.Points.Count).Sum();

            foreach (var item in lineSegements)
            {
                if (EdgeSimplfy > 0)
                    LineSegmentSimplifier.SimplifySameGradientPoints(item, EdgeSimplfy);
                if (EdgeCloseSimplfy > 0)
                    LineSegmentSimplifier.SimplifyTooClosePoints(item, EdgeCloseSimplfy * item.CalculateBound().Width);
            }

            LineSegments.Clear();

            foreach (var segments in lineSegements)
            {
                var geometry = new PathGeometry();
                var figure = new PathFigure();

                figure.IsClosed = false;

                var firstPoint = segments.Points.FirstOrDefault();
                figure.StartPoint = new(firstPoint.X, firstPoint.Y);

                foreach (var point in segments.Points)
                    figure.Segments.Add(new LineSegment(new(point.X, point.Y), true));

                geometry.Figures.Add(figure);
                LineSegments.Add(new LineSegmentWrapper()
                {
                    Color = new SolidColorBrush(Color.FromArgb(segments.Color.A, segments.Color.R, segments.Color.G, segments.Color.B)),
                    Path = geometry,
                    RawLineSegmentCollection = segments
                });
            }


            //calc bound
            (var minX, var maxX) = lineSegements.SelectMany(x => x.Points).Select(x => x.X).MaxMinBy();
            (var minY, var maxY) = lineSegements.SelectMany(x => x.Points).Select(x => x.Y).MaxMinBy();

            var x = minX;
            var y = minY;
            var size = (GetView() as SvgToLaneBrowserView).PathCanvas.RenderSize;
            var width = maxX - minX;
            var height = maxY - minY;

            CurrentSegmentsBound = new Rect(x, y, width, height);
            CanvasTranslateOffset = new Point((int)(size.Width / 2 - width / 2), (int)(size.Height / 2 - height / 2));

            UpdateHorizonVerticalAlignPreview();
            UpdateLinesLaneTarget();

            NotifyOfPropertyChange(() => AfterOptimzePointsCount);
        }

        public void UpdateLinesLaneTarget()
        {
            var greenHue = System.Drawing.Color.Green.GetHue();
            var redHue = System.Drawing.Color.Red.GetHue();
            var blueHue = System.Drawing.Color.Blue.GetHue();

            bool IsEqualOrSimilar(Color color, float targetColorHue)
            {
                var colorHue = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B).GetHue();
                return Math.Abs(colorHue - targetColorHue) <= (120 * SolorSimilar / 100);
            }

            foreach (LineSegmentWrapper wrapper in LineSegments)
            {
                if (IsEqualOrSimilar(wrapper.Color.Color, greenHue))
                    wrapper.LaneTarget = LaneType.Center;
                else if (IsEqualOrSimilar(wrapper.Color.Color, redHue))
                    wrapper.LaneTarget = LaneType.Left;
                else if (IsEqualOrSimilar(wrapper.Color.Color, blueHue))
                    wrapper.LaneTarget = LaneType.Right;
                else
                    wrapper.LaneTarget = null;
            }

            NotifyOfPropertyChange(() => OutputOptimzePointsCount);
        }

        public void UpdateHorizonVerticalAlignPreview()
        {
            var applyWidth = PointHorizonAlignTarget switch
            {
                PointHorizonAlignTarget.Left => 0,
                PointHorizonAlignTarget.Center => CurrentSegmentsBound.Width / 2,
                PointHorizonAlignTarget.Right => CurrentSegmentsBound.Width,
                _ => 0
            };

            var applyHeight = PointVerticalAlignTarget switch
            {
                PointVerticalAlignTarget.Top => 0,
                PointVerticalAlignTarget.Center => CurrentSegmentsBound.Height / 2,
                PointVerticalAlignTarget.Bottom => CurrentSegmentsBound.Height,
                _ => 0
            };

            CurrentOriginOffset = new Point(CanvasTranslateOffset.X + applyWidth, CanvasTranslateOffset.Y + applyHeight);
        }

        public void OutputToEditor()
        {
            var laneTargets = LineSegments
                .Select(x => (x.RawLineSegmentCollection.Points, x.LaneTarget))
                .SelectMany(x => PointReforger.ReforgeAsUnidirectional(x.Points).Select(y => (x.LaneTarget, y.ToList())));

            foreach ((var laneType, var lineSegment) in laneTargets.Where(x => x.Item2.Count >= 2))
            {
                if (MathUtils.calcGradient(lineSegment[0].X, lineSegment[1].X, lineSegment[0].Y, lineSegment[1].Y) < 0)
                    lineSegment.Reverse();

                if (GenerateLaneViewModel(lineSegment, laneType) is DisplayObjectViewModelBase generatedViewModel)
                    Editor.AddObject(generatedViewModel);
            }

            Editor.Redraw(RedrawTarget.OngekiObjects);
        }

        private IEnumerable<(TGrid tGrid, XGrid xGrid, bool? status)> EnumMappedPositionLineSegements(List<System.Drawing.PointF> lineSegment)
        {
            var bx = XGridCalculator.ConvertXGridToX(XGrid, Editor);
            var by = TGridCalculator.ConvertTGridToY(TGrid, Editor);

            return lineSegment.Select((x, i) =>
            {
                var rx = bx - (CurrentOriginOffset.X - x.X);
                var ry = by + (CurrentOriginOffset.Y - CurrentSegmentsBound.Height + CanvasTranslateOffset.Y / 2 - x.Y);

                var rTGrid = TGridCalculator.ConvertYToTGrid(ry, Editor);
                var rXGrid = XGridCalculator.ConvertXToXGrid(rx, Editor);

                var status = (bool?)null;

                if (i == 0)
                    status = true;
                else if (i == lineSegment.Count - 1)
                    status = false;

                return (rTGrid, rXGrid, status);
            });
        }

        private DisplayObjectViewModelBase GenerateLaneViewModel(List<System.Drawing.PointF> lineSegment, LaneType? laneType)
        {
            var xtGrids = EnumMappedPositionLineSegements(lineSegment);

            var laneStartViewModel = laneType switch
            {
                LaneType.Left => new LaneLeftStartViewModel(),
                LaneType.Center => new LaneCenterStartViewModel(),
                LaneType.Right => new LaneRightStartViewModel(),
                _ => default(DisplayObjectViewModelBase)
            };

            var leneStartObj = laneStartViewModel.ReferenceOngekiObject as LaneStartBase;

            var nextGenetor = laneType switch
            {
                LaneType.Left => () => new LaneLeftNext(),
                LaneType.Center => () => new LaneCenterNext(),
                LaneType.Right => () => new LaneRightNext(),
                _ => default(Func<ConnectableChildObjectBase>)
            };

            var endGenetor = laneType switch
            {
                LaneType.Left => () => new LaneLeftEnd(),
                LaneType.Center => () => new LaneCenterEnd(),
                LaneType.Right => () => new LaneRightEnd(),
                _ => default(Func<ConnectableChildObjectBase>)
            };

            if (laneStartViewModel is null)
                return null;

            foreach ((var tGrid, var xGrid, var status) in xtGrids)
            {
                if (status == true)
                {
                    leneStartObj.XGrid = xGrid;
                    leneStartObj.TGrid = tGrid;
                    continue;
                }

                var obj = status == null ? nextGenetor() : endGenetor();
                leneStartObj.AddChildObject(obj);

                obj.XGrid = xGrid;
                obj.TGrid = tGrid;
            }

            return laneStartViewModel;
        }
    }
}
