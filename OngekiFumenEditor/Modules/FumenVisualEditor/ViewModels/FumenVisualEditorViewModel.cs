using Caliburn.Micro;
using Gemini.Framework;
using OngekiFumenEditor.Modules.FumenVisualEditor.Controls;
using OngekiFumenEditor.Modules.FumenVisualEditor.Controls.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    [Export(typeof(FumenVisualEditorViewModel))]
    public class FumenVisualEditorViewModel : PersistedDocument
    {
        public FumenVisualEditorView View { get; private set; }
        public Panel VisualDisplayer => View?.VisualDisplayer;

        protected override void OnViewLoaded(object v)
        {
            base.OnViewLoaded(v);
            var view = v as FumenVisualEditorView;

            View = view;
        }

        private Task InitalizeVisualData()
        {
            return Task.CompletedTask;
        }

        protected override async Task DoLoad(string filePath)
        {
            Log.LogInfo($"FumenVisualEditorViewModel DoLoad()");
            await InitalizeVisualData();
        }

        protected override async Task DoNew()
        {
            Log.LogInfo($"FumenVisualEditorViewModel DoNew()");
            await InitalizeVisualData();
        }

        protected override async Task DoSave(string filePath)
        {
            Log.LogInfo($"FumenVisualEditorViewModel DoSave()");
            await InitalizeVisualData();
        }

        public void OnNewObjectAdd(OngekiObjectViewModelBase viewModel)
        {
            var view = ViewCreateHelper.CreateView(viewModel);
            VisualDisplayer.Children.Add(view);

            Log.LogInfo($"create new display object: {viewModel.ReferenceOngekiObject.Name}");
        }
    }
}
