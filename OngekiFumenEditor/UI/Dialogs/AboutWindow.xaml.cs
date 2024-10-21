using MahApps.Metro.Controls;
using OngekiFumenEditor.Utils;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace OngekiFumenEditor.UI.Dialogs
{
    /// <summary>
    /// AboutWindow.xaml 的交互逻辑
    /// </summary>
    public partial class AboutWindow : MetroWindow
    {
        public string CommitHash => ThisAssembly.GitCommitId[..7];
        public string Version => typeof(AppBootstrapper).Assembly.GetName().Version.ToString();
        public string ProductVersion => ThisAssembly.AssemblyFileVersion.Split("+").FirstOrDefault();
        public string BuildTime => typeof(AboutWindow).Assembly
                                .GetCustomAttributes<AssemblyMetadataAttribute>()
                                .FirstOrDefault(x => x.Key == "BuildDateTime")
                                ?.Value;
        public string BuildConfiguration => ThisAssembly.AssemblyConfiguration;
        public string CommitDate => ThisAssembly.GitCommitDate.AddHours(8).ToString("yyyy/M/dd H:mm:ss.fff");

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
