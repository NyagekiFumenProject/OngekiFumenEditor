using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views.EditorObjects;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.Attributes;
using System;
using System.ComponentModel;
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

    public abstract class ConnectorViewModel<T> : ConnectorViewModel where T : IDisplayableObject, IHorizonPositionObject, ITimelineObject
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
                Set(ref connector, value);
            }
        }

        private void OnLanePropChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LaneConnector.LineColor))
            {
                NotifyOfPropertyChange(() => LineBrush);
            }
        }

        public override IDisplayableObject DisplayableObject => Connector;

        public virtual Brush LineBrush { get; } = Brushes.White;
        public virtual DoubleCollection LineDashArray { get; } = new DoubleCollection() { 10, 0 };
        public virtual int LineThickness { get; } = 1;

        public override void OnObjectCreated(object createFrom, FumenVisualEditorViewModel editorViewModel)
        {
            if (createFrom is ConnectorLineBase<T> connector)
                Connector = connector;
            EditorViewModel = editorViewModel;
        }
    }
}
