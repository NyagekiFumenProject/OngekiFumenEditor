using Caliburn.Micro;
using OngekiFumenEditor.Base.OngekiObjects;
using SimpleSvg2LineSegementInterpolater.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.SvgToLaneBrowser.ViewModels
{
    public class LineSegmentWrapper : PropertyChangedBase
    {
        private PathGeometry path;
        public PathGeometry Path
        {
            get => path;
            set => Set(ref path, value);
        }

        private SolidColorBrush color;
        public SolidColorBrush Color
        {
            get => color;
            set => Set(ref color, value);
        }

        public Brush LaneTargetColor => LaneTarget switch
        {
            LaneType.Left => Brushes.Red,
            LaneType.Center => Brushes.Green,
            LaneType.Right => Brushes.Blue,
            _ => Brushes.Transparent
        };

        private LineSegementCollection rawLineSegmentCollection;
        public LineSegementCollection RawLineSegmentCollection
        {
            get => rawLineSegmentCollection;
            set => Set(ref rawLineSegmentCollection, value);
        }

        private LaneType? laneTarget = null;
        public LaneType? LaneTarget
        {
            get => laneTarget;
            set
            {
                Set(ref laneTarget, value);
                NotifyOfPropertyChange(() => LaneTargetColor);
            }
        }
    }

}
