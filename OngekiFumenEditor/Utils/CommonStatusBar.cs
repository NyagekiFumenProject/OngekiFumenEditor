using Gemini.Modules.StatusBar;
using Gemini.Modules.StatusBar.ViewModels;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;

namespace OngekiFumenEditor.Utils
{
	[Export(typeof(CommonStatusBar))]
	[PartCreationPolicy(CreationPolicy.Shared)]
	public class CommonStatusBar
	{
		private IStatusBar statusBar;

		public StatusBarItemViewModel MainContentViewModel => statusBar.Items.ElementAtOrDefault(0);
		public StatusBarItemViewModel SubLeftContentViewModel => statusBar.Items.ElementAtOrDefault(1);
		public StatusBarItemViewModel SubRightMainContentViewModel => statusBar.Items.ElementAtOrDefault(2);

		[ImportingConstructor]
		public CommonStatusBar(IStatusBar statusBar)
		{
			this.statusBar = statusBar;

			statusBar.AddItem("", new GridLength(1, GridUnitType.Star));
			statusBar.AddItem("", new GridLength(100));
			statusBar.AddItem("", new GridLength());
		}
	}
}
