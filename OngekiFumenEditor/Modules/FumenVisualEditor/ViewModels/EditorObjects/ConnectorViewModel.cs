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
            var resT = Connector.From.TGrid.ResT;
            var resX = Connector.From.XGrid.ResX;
            void addPoint(Vector2 gv2)
            {
                var y = (float)(EditorViewModel.TotalDurationHeight - TGridCalculator.ConvertTGridToY(new(gv2.Y / resT, 0), EditorViewModel));
                var x = (float)XGridCalculator.ConvertXGridToX(new(gv2.X / resX, 0), EditorViewModel);

                var seg = new LineSegment(new(x + 0.0000001 * MathUtils.Random(100), y), true);
                seg.Freeze();
                Lines.Add(seg);
            }

            Lines.Clear();

            var isVaild = true;

            if (Connector.To is ConnectableChildObjectBase childObject)
            {
                foreach ((var gridVec2, var iv) in childObject.GenPath())
                {
                    isVaild = isVaild && iv;
                    addPoint(gridVec2);
                }
            }
            else
            {
                addPoint(new(Connector.From.XGrid.TotalGrid, Connector.From.TGrid.TotalGrid));
                addPoint(new(Connector.To.XGrid.TotalGrid, Connector.To.TGrid.TotalGrid));
            }

            LineDashArray = isVaild ? DefaultLineDashArray : InvaildCurveLineDashArray;
            NotifyOfPropertyChange(() => Lines);
        }
    }
}
