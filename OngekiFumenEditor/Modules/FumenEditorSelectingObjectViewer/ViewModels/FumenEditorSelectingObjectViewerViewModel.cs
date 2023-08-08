using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.ViewModels
{
    [Export(typeof(IFumenEditorSelectingObjectViewer))]
    public class FumenEditorSelectingObjectViewerViewModel : Tool, IFumenEditorSelectingObjectViewer
    {
        private FumenVisualEditorViewModel editor;
        public FumenVisualEditorViewModel Editor
        {
            get => editor;
            set
            {
                Set(ref editor, value);
            }
        }

        public override PaneLocation PreferredLocation => PaneLocation.Bottom;

        public FumenEditorSelectingObjectViewerViewModel()
        {
            DisplayName = $"当前选择物件查看器";
            IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;
            Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
        }

        private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
        {
            Editor = @new;
        }

        public void OnRefresh()
        {
            Editor?.NotifyOfPropertyChange(nameof(Editor.SelectObjects));
        }

        public void OnItemSingleClick(OngekiObjectBase item)
        {
            if (Editor is null)
                return;

            //Editor.SelectObjects.Where(x => x != item).FilterNull().ForEach(x => x.IsSelected = false);
            //Editor.SelectObjects.Where(x => x == item).FilterNull().ForEach(x => x.IsSelected = true);

            IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(new[] { item as ISelectableObject }.FilterNull(), Editor);
        }

        public void OnItemDoubleClick(OngekiObjectBase item)
        {
            if (Editor is null)
                return;

            if (item is ITimelineObject timelineObject)
                Editor.ScrollTo(timelineObject.TGrid);

            Editor.SelectObjects.Where(x => x != item).FilterNull().ForEach(x => x.IsSelected = false);
            IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(Editor);
        }
    }
}
