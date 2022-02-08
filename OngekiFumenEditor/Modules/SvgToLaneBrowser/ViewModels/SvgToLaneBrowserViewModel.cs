using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using Microsoft.Win32;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.SvgToLaneBrowser.Views;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using SimpleSvg2LineSegementInterpolater;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.SvgToLaneBrowser.ViewModels
{
    [Export(typeof(ISvgToLaneBrowser))]
    public class SvgToLaneBrowserViewModel : WindowBase, ISvgToLaneBrowser
    {
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
            }
        }

        private PointAlignTarget pointAlignTarget;
        public PointAlignTarget PointAlignTarget
        {
            get => pointAlignTarget;
            set
            {
                Set(ref pointAlignTarget, value);
            }
        }

        public IEnumerable<PointAlignTarget> PointAlignTargetValues => Enum.GetValues<PointAlignTarget>();

        public class LineSegmentWrapper : PropertyChangedBase
        {
            private PathGeometry path;
            public PathGeometry Path
            {
                get => path;
                set => Set(ref path, value);
            }

            private System.Windows.Media.Brush color;
            public System.Windows.Media.Brush Color
            {
                get => color;
                set => Set(ref color, value);
            }
        }

        public ObservableCollection<LineSegmentWrapper> LineSegments { get; } = new ObservableCollection<LineSegmentWrapper>();

        private Point canvasTranslateOffset;
        public Point CanvasTranslateOffset
        {
            get => canvasTranslateOffset;
            set
            {
                Set(ref canvasTranslateOffset, value);
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
            var lineSegements = (await Interpolater.GenerateInterpolatedLineSegmentAsync(await File.ReadAllTextAsync(SvgFilePath), new InterpolaterOption()
            {

            })).Where(x => x.Points.Count > 0).ToList();

            foreach (var item in lineSegements)
                LineSegmentOptimzer.Optimze(item);

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
                    Color = new SolidColorBrush(System.Windows.Media.Color.FromArgb(segments.Color.A, segments.Color.R, segments.Color.G, segments.Color.B)),
                    Path = geometry
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

            CanvasTranslateOffset = new Point((int)(size.Width / 2 - width / 2), (int)(size.Height / 2 - height / 2));
        }
    }
}
