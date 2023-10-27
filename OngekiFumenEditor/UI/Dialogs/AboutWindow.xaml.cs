using MahApps.Metro.Controls;
using OngekiFumenEditor.Utils;
using System.Diagnostics;
using System.Linq;

namespace OngekiFumenEditor.UI.Dialogs
{
	/// <summary>
	/// AboutWindow.xaml 的交互逻辑
	/// </summary>
	public partial class AboutWindow : MetroWindow
	{
		public string productVersionStr => FileVersionInfo.GetVersionInfo(typeof(AppBootstrapper).Assembly.Location).ProductVersion;

		public string CommitHash => productVersionStr.Split("+").LastOrDefault();
		public string Version => typeof(AppBootstrapper).Assembly.GetName().Version.ToString();
		public string ProductVersion => productVersionStr.Split("+").FirstOrDefault();

		public AboutWindow()
		{
			InitializeComponent();
			DataContext = this;
		}

		private void Label_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
		{
			ProcessUtils.OpenUrl(e.Uri.ToString());
		}
	}
}
