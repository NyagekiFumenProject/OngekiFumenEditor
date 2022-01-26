using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
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
            Fumen = null;
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
