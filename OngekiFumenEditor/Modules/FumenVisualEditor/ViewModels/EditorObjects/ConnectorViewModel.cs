using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views.EditorObjects;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.Attributes;
using System;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects
{
    [MapToView(ViewType = typeof(ConnectorView))]
    public abstract class ConnectorViewModel : PropertyChangedBase, IEditorDisplayableViewModel
    {
        public int RenderOrderZ => 2;
        public bool NeedCanvasPointsBinding => false;

        public abstract void OnObjectCreated(object createFrom, FumenVisualEditorViewModel editorViewModel);
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
                Set(ref connector, value);
            }
        }

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

        public abstract Brush LineBrush { get; }

        public override void OnObjectCreated(object createFrom, FumenVisualEditorViewModel editorViewModel)
        {
            if (createFrom is ConnectorLineBase<T> connector)
                Connector = connector;
            EditorViewModel = editorViewModel;
        }
    }
}
