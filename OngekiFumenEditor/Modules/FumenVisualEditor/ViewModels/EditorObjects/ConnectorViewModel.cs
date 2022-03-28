using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views.EditorObjects;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.Attributes;
using OpenTK.Mathematics;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects
{
    [MapToView(ViewType = typeof(ConnectorView))]
    public abstract class ConnectorViewModel : PropertyChangedBase, IEditorDisplayableViewModel
    {
        public int RenderOrderZ => 2;
        public bool NeedCanvasPointsBinding => false;

        public abstract IDisplayableObject DisplayableObject { get; }

        private FumenVisualEditorViewModel editorViewModel;
        public FumenVisualEditorViewModel EditorViewModel
        {
            get
            {
                return editorViewModel;
            }
            set
            {
                Set(ref editorViewModel, value);
            }
        }

        public abstract void OnObjectCreated(object createFrom, FumenVisualEditorViewModel editorViewModel);

        public virtual void OnEditorRedrawObjects()
        {
            NotifyOfPropertyChange(() => EditorViewModel);
        }
    }

    public abstract class ConnectorViewModel<T> : ConnectorViewModel where T : IDisplayableObject, IHorizonPositionObject, ITimelineObject, INotifyPropertyChanged
    {
        private ConnectorLineBase<T> connector;
        public ConnectorLineBase<T> Connector
        {
            get
            {
                return connector;
            }
            set
            {
                this.RegisterOrUnregisterPropertyChangeEvent(Connector, value, OnLanePropChanged);

                if (Connector is not null)
                {
                    Connector.From.PropertyChanged -= OnLanePropChanged;
                    Connector.To.PropertyChanged -= OnLanePropChanged;
                }

                if (value is not null)
                {
                    value.From.PropertyChanged += OnLanePropChanged;
                    value.To.PropertyChanged += OnLanePropChanged;
                }

                Set(ref connector, value);
            }
        }

        private void From_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnLanePropChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(LaneConnector.LineColor):
                    NotifyOfPropertyChange(() => LineBrush);
                    break;
                case nameof(TGrid):
                case nameof(XGrid):
                case nameof(ConnectableChildObjectBase.CurvePrecision):
                case nameof(ConnectableChildObjectBase.PathControls):
                    RebuildLines();
                    break;
                default:
                    break;
            }
        }

        public override IDisplayableObject DisplayableObject => Connector;

        public virtual Brush LineBrush { get; } = Brushes.White;

        public static DoubleCollection StaticDefaultLineDashArray { get; } = new() { 10, 0 };
        public static DoubleCollection StaticInvaildCurveLineDashArray { get; } = new() { 10, 5 };

        public virtual DoubleCollection DefaultLineDashArray => StaticDefaultLineDashArray;
        public virtual DoubleCollection InvaildCurveLineDashArray => StaticInvaildCurveLineDashArray;

        private DoubleCollection lineDashArray;
        public DoubleCollection LineDashArray
        {
            get => lineDashArray;
            set => Set(ref lineDashArray, value);
        }

        public virtual int LineThickness { get; } = 1;
        public PathSegmentCollection Lines { get; set; } = new PathSegmentCollection();

        public override void OnObjectCreated(object createFrom, FumenVisualEditorViewModel editorViewModel)
        {
            if (createFrom is ConnectorLineBase<T> connector)
                Connector = connector;
            EditorViewModel = editorViewModel;
            RebuildLines();
        }

        private void RebuildLines()
        {
            Vector2 getPoint<X>(X o) where X : IHorizonPositionObject, ITimelineObject
            {
                var y = (float)(EditorViewModel.TotalDurationHeight - TGridCalculator.ConvertTGridToY(o.TGrid, EditorViewModel));
                var x = (float)XGridCalculator.ConvertXGridToX(o.XGrid, EditorViewModel);

                return new(x, y);
            }

            int calcSign(Vector2 a, Vector2 b)
            {
                return Math.Sign(a.Y - b.Y);
            }

            void addPoint(Vector2 point)
            {
                var seg = new LineSegment(new(point.X + 0.0000001 * MathUtils.Random(100), point.Y), true);
                seg.Freeze();
                Lines.Add(seg);
            }

            Lines.Clear();

            var fromPoint = getPoint(Connector.From);
            var midPoint = (Connector.To as ConnectableChildObjectBase)?.PathControls.Select(x => getPoint(x)) ?? Enumerable.Empty<Vector2>();
            var toPoint = getPoint(Connector.To);
            var step = (Connector.To as ConnectableChildObjectBase)?.CurvePrecision ?? 2857;
            using var d = midPoint.Prepend(fromPoint).Append(toPoint).ToListWithObjectPool(out var points);

            var prevP = points[0];
            var prevSign = 0;
            var isVaild = true;

            if (points.Count > 2)
            {
                var t = 0f;

                while (true)
                {
                    var p = BezierCurve.CalculatePoint(points, t);
                    var sign = calcSign(prevP, p);

                    if (isVaild && prevSign != sign && prevSign * sign != 0)
                        isVaild = false;
                    addPoint(p);

                    if (t >= 1)
                        break;

                    t = MathF.Min(1, t + step);
                    prevP = p;
                    prevSign = sign;
                }
                LineDashArray = isVaild ? DefaultLineDashArray : InvaildCurveLineDashArray;
            }
            else
            {
                addPoint(points[0]);
                addPoint(points[1]);
                LineDashArray = DefaultLineDashArray;
            }

            NotifyOfPropertyChange(() => Lines);
        }
    }
}
