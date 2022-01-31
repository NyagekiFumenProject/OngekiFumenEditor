using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.ViewModels
{
    [Export(typeof(IFumenEditorSelectingObjectViewer))]
    public class FumenEditorSelectingObjectViewerViewModel : Tool, IFumenEditorSelectingObjectViewer
    {
        private FumenVisualEditorViewModel editor;
        public FumenVisualEditorViewModel Editor
        {
            get => editor;
            set => Set(ref editor, value);
        }

        private DisplayObjectViewModelBase currentPickedSelectObject;
        public DisplayObjectViewModelBase CurrentPickedSelectObject
        {
            get => currentPickedSelectObject;
            set
            {
                Set(ref currentPickedSelectObject, value);
                IoC.Get<IFumenObjectPropertyBrowser>().SetCurrentOngekiObject(value?.ReferenceOngekiObject, Editor);
            }
        }

        public override PaneLocation PreferredLocation => PaneLocation.Bottom;

        public FumenEditorSelectingObjectViewerViewModel()
        {
            DisplayName = "当前选择物件查看器";
            IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;
        }

        private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
        {
            Editor = @new;
            CurrentPickedSelectObject = null;
        }
    }
}
