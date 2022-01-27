using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenMetaInfoBrowser.ViewModels
{
    [Export(typeof(IFumenMetaInfoBrowser))]
    public class FumenMetaInfoBrowserViewModel : Tool, IFumenMetaInfoBrowser
    {
        public FumenMetaInfoBrowserViewModel()
        {
            DisplayName = "谱面信息";
            IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;
        }

        private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
        {
            Fumen = @new?.Fumen;
            this.RegisterOrUnregisterPropertyChangeEvent(old, @new, OnEditorPropertyChanged);
        }

        private void OnEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FumenVisualEditorViewModel.Fumen))
                Fumen = (sender as FumenVisualEditorViewModel).Fumen;
        }

        public override PaneLocation PreferredLocation => PaneLocation.Right;

        private string errorMessage;
        public string ErrorMessage
        {
            get { return errorMessage; }
            set
            {
                errorMessage = value;
                if (!string.IsNullOrWhiteSpace(value))
                    Log.LogError("Current error message : " + value);
                NotifyOfPropertyChange(() => ErrorMessage);
            }
        }

        private OngekiFumen fumen;
        public OngekiFumen Fumen
        {
            get
            {
                return fumen;
            }
            set
            {
                fumen = value;
                NotifyOfPropertyChange(() => Fumen);
                if (Fumen is null)
                {
                    FumenProxy = null;
                    ErrorMessage = "Please load/new a fumen document first.";
                }
                else
                {
                    ErrorMessage = null;
                    FumenProxy = new OngekiFumenModelProxy(Fumen);
                }
            }
        }

        private OngekiFumenModelProxy fumenProxy;
        public OngekiFumenModelProxy FumenProxy
        {
            get
            {
                return fumenProxy;
            }
            set
            {
                fumenProxy = value;
                NotifyOfPropertyChange(() => FumenProxy);
            }
        }
    }
}
