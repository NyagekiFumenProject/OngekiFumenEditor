using Gemini.Modules.StatusBar;
using Gemini.Modules.StatusBar.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Utils
{
    [Export(typeof(CommonStatusBar))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CommonStatusBar
    {
        public IStatusBar StatusBar { get; private set; }

        public StatusBarItemViewModel MainContentViewModel => StatusBar.Items.ElementAtOrDefault(0);
        public StatusBarItemViewModel SubLeftContentViewModel => StatusBar.Items.ElementAtOrDefault(1);
        public StatusBarItemViewModel SubRightMainContentViewModel => StatusBar.Items.ElementAtOrDefault(2);

        [ImportingConstructor]
        private CommonStatusBar(IStatusBar statusBar)
        {
            StatusBar = statusBar;

            StatusBar.AddItem("", new GridLength(1, GridUnitType.Star));
            StatusBar.AddItem("", new GridLength(100));
            StatusBar.AddItem("", new GridLength(100));
        }
    }
}
