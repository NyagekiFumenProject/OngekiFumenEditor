using Gemini.Framework;
using OngekiFumenEditor.Modules.FumenVisualEditor.ToolboxItems;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    [Export(typeof(FumenVisualEditorViewModel))]
    public class FumenVisualEditorViewModel : PersistedDocument
    {
        private FumenVisualEditorView view;

        private ObservableCollection<ElementViewModel> elements = new ObservableCollection<ElementViewModel>();
        public ObservableCollection<ElementViewModel> Elements
        {
            get
            {
                return elements;
            }
            set
            {
                elements = value;
                NotifyOfPropertyChange(() => Elements);
            }
        }

        protected override void OnViewLoaded(object v)
        {
            base.OnViewLoaded(v);
            var view = v as FumenVisualEditorView;

            this.view = view;
        }

        private void InitalizeVisualData()
        {
            Elements.Clear();
            Elements = new ObservableCollection<ElementViewModel>();
        }

        protected override async Task DoLoad(string filePath)
        {
            Log.LogInfo($"FumenVisualEditorViewModel DoLoad()");
            InitalizeVisualData();
        }

        protected override async Task DoNew()
        {
            Log.LogInfo($"FumenVisualEditorViewModel DoNew()");
            InitalizeVisualData();
        }

        protected override async Task DoSave(string filePath)
        {
            Log.LogInfo($"FumenVisualEditorViewModel DoSave()");
            InitalizeVisualData();
        }
    }
}
