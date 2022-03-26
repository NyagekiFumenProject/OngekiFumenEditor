using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.Lane;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views.EditorObjects;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.Attributes;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects
{
    [MapToView(ViewType = typeof(LaneCurveObjectConnectorView))]
    public class LaneCurveObjectConnectorViewModel : ConnectorViewModel
    {
        public override IDisplayableObject DisplayableObject => Connector;

        private LaneCurveObjectConnector connector;
        public LaneCurveObjectConnector Connector
        {
            get => connector;
            set
            {
                if (connector is not null)
                {
                    connector.From.PropertyChanged -= Value_PropertyChanged;
                    connector.To.PropertyChanged -= Value_PropertyChanged;
                }
                if (value is not null)
                {
                    value.From.PropertyChanged += Value_PropertyChanged;
                    value.To.PropertyChanged += Value_PropertyChanged;
                }

                Set(ref connector, value);//todo listen two point changing
            }
        }

        private void Value_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(TGrid):
                case nameof(XGrid):
                    RebuildLines();
                    break;
                default:
                    break;
            }
        }

        public override void OnObjectCreated(object createFrom, FumenVisualEditorViewModel editorViewModel)
        {
            if (createFrom is LaneCurveObjectConnector connector)
                Connector = connector;
            EditorViewModel = editorViewModel;
            RebuildLines();
        }

        private void RebuildLines()
        {
            Vector2 getPoint(OngekiMovableObjectBase o)
            {
                var y = (float)(EditorViewModel.TotalDurationHeight - TGridCalculator.ConvertTGridToY(o.TGrid, EditorViewModel));
                var x = (float)XGridCalculator.ConvertXGridToX(o.XGrid, EditorViewModel);

                return new(x, y);
            }

            Lines.Clear();

            var fromPoint = getPoint(Connector.From);
            var midPoint = getPoint((LaneCurveObject)Connector.To);
            midPoint = new Vector2(midPoint.X + 100, midPoint.Y);
            var toPoint = getPoint(Connector.To);

            var bezierCurve = new BezierCurve(fromPoint, midPoint, toPoint);
            for (var t = 0f; t <= 1; t += 0.01f)
            {
                var point = bezierCurve.CalculatePoint(t);
                var seg = new LineSegment(new(point.X + 0.0000001 * MathUtils.Random(100), point.Y), true);
                seg.Freeze();
                Lines.Add(seg);
            }

            NotifyOfPropertyChange(() => Lines);
        }

        private Brush lineBrush;
        public Brush LineBrush
        {
            get => lineBrush;
            set => Set(ref lineBrush, value);
        }

        public PathSegmentCollection Lines { get; set; } = new PathSegmentCollection();
    }
}
