using Caliburn.Micro;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Base
{
    public class BeamConnectorViewModel : PropertyChangedBase, IEditorDisplayableViewModel
    {
        private BeamConnector connector;
        public BeamConnector Connector
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

        public void OnObjectCreated(object createFrom, FumenVisualEditorViewModel editorViewModel)
        {
            if (createFrom is BeamConnector connector)
                Connector = connector;
            EditorViewModel = editorViewModel;
        }
    }
}
