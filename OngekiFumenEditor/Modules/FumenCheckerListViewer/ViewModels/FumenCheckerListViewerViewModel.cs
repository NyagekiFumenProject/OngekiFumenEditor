using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.ViewModels
{
    [Export(typeof(IFumenCheckerListViewer))]
    public class FumenCheckerListViewerViewModel : Tool, IFumenCheckerListViewer
    {
        public override PaneLocation PreferredLocation => PaneLocation.Bottom;

        public ObservableCollection<ICheckResult> CheckResults { get; } = new ObservableCollection<ICheckResult>();

        private FumenVisualEditorViewModel editor = default;
        public FumenVisualEditorViewModel Editor
        {
            get => editor;
            set { 
                Set(ref editor, value);
                RefreshCurrentFumen();
            }
        }

        private List<IFumenCheckRule> checkRules;

        public FumenCheckerListViewerViewModel()
        {
            DisplayName = "谱面检查器";
            checkRules = IoC.GetAll<IFumenCheckRule>().ToList();
            IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += (n, o) => Editor = n;
            Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
        }

        public void OnItemDoubleClick(ICheckResult checkResult)
        {
            checkResult?.Navigate(Editor);
        }

        public void RefreshCurrentFumen()
        {
            CheckResults.Clear();

            if (Editor?.Fumen is not null)
            {
                var fumen = Editor.Fumen;

                foreach (var checkRule in checkRules.SelectMany(x => x.CheckRule(fumen, Editor)))
                {
                    CheckResults.Add(checkRule);
                }
            }
        }
    }
}
